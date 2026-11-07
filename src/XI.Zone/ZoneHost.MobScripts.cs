using System;
using System.Collections.Generic;
using System.IO;
using XI.Map;
using XI.Script;

namespace XI.Zone
{
    // Mob scripts: luautils::OnEntityLoad caches scripts/zones/<zone>/mobs/<name>.lua at xi.zones.<zone>.mobs.<name>,
    // and the hooks run from it (OnMobInitialize at load, OnMobSpawn, OnMobEngage, OnMobFight, OnMobRoam,
    // OnMobDisengage, OnMobDeath through InteractionGlobal, OnMobDespawn); ApplyMixins / ApplyZoneMixins run the
    // mixins the mob's script and scripts/mixins/zones/<zone>.lua name, and CAIEventHandler holds the listeners they
    // add. A pet's script is xi.pets.<type>, a trust's xi.actions.spells.trust.<name>. OnMobSpawnCheck is asked before
    // a respawn (MobSpawnHandler, SpawnSlot). Elsewhere: an instance's mobs (ZoneHost.Instances.cs runs
    // InitMobScripts), onMobRoamAction (ZoneHost.MobRoam.cs), onMobFollow / onMobUnfollow (ZoneHost.MobFollow.cs),
    // onCriticalHit (ZoneHost.Defense.cs, Ranged.cs, WeaponSkillAction.cs), the link parties (ZoneHost.MobLink.cs).
    public sealed partial class ZoneHost
    {
        // the mob scripts that set mixins, and the zones with a zone mixin file
        private readonly Dictionary<string, bool> scriptHasMixins_ = new Dictionary<string, bool>(StringComparer.Ordinal);

        private string MobScriptFile(MobEntity m) => m switch
        {
            PetEntity pet => $"./scripts/globals/pets/{pet.ScriptName}.lua",
            TrustEntity trust => TrustScriptFile(trust),
            _ => $"./scripts/zones/{AreaOf(m).Name}/mobs/{m.Name}.lua",
        };

        // getEntityCachedFunction: a mob's script function, a pet's at xi.pets.<type>, a trust's at
        // xi.actions.spells.trust.<name>; nil when there is none
        private void PushMobFunction(MobEntity m, string fn)
        {
            if (m is TrustEntity)
            {
                scripts_.PushFileFunction(MobScriptFile(m), fn);
                return;
            }
            if (m is not PetEntity pet)
            {
                scripts_.PushFileFunction(MobScriptFile(m), fn);
                return;
            }
            lua_.GetPath($"xi.pets.{pet.ScriptName}.{fn}");
            if (lua_.IsFunction(-1)) return;
            lua_.Pop(1);
            lua_.PushNil();
        }

        private static AiEventHandler EventsOf(object e) => e is BattleEntity b ? b.Events : ((NpcEntity)e).Events;

        // CAIEventHandler::triggerListener on the entity, with these arguments
        private void TriggerListener(object entity, string eventName, params object[] args)
        {
            if (lua_ == null) return;
            EventsOf(entity).Trigger(lua_, eventName, () =>
            {
                foreach (var a in args) lua_.Push(a);
                return args.Length;
            }, msg => log_("lua: " + msg));
        }

        // getEntityCachedFunction and the call: false when the script has no such function
        private bool CallMobHook(MobEntity m, string fn, params object[] args)
        {
            if (lua_ == null) return false;
            int top = lua_.Top;
            try
            {
                PushMobFunction(m, fn);
                if (lua_.IsNil(-1)) return false;
                foreach (var a in args) lua_.Push(a);
                lua_.Call(args.Length, 0);
            }
            catch (LuaException e)
            {
                ScriptError("luautils::" + fn, e);
            }
            finally
            {
                lua_.SetTop(top);
            }
            return true;
        }

        // the load half of zoneutils::LoadMOBList: each mob's script cached, then OnMobInitialize and the mixins
        private void InitMobScripts(ZoneInfo info)
        {
            if (scripts_ == null)
            {
                foreach (var m in info.Mobs) MobLinkFindParty(info, m);
                return;
            }
            var loaded = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in info.Mobs)
            {
                string file = MobScriptFile(m);
                if (loaded.Add(file)) scripts_.LoadLuaObjectFromFile(file);
            }
            foreach (var m in info.Mobs)
            {
                CallMobHook(m, "onMobInitialize", m);
                MobLinkFindParty(info, m);
                ApplyMixins(m, MobScriptFile(m));
                ApplyMixins(m, $"./scripts/mixins/zones/{info.Name}.lua");
            }
        }

        // luautils::ApplyMixins / ApplyZoneMixins: the file run again for the mixins (and mixinOptions) it sets,
        // and the global applyMixins over them. A file that never names mixins is not run again.
        private void ApplyMixins(MobEntity m, string file)
        {
            if (!scriptHasMixins_.TryGetValue(file, out bool hasMixins))
                scriptHasMixins_[file] = hasMixins = File.Exists(file) && File.ReadAllText(file).Contains("mixins");
            if (!hasMixins) return;

            int top = lua_.Top;
            try
            {
                lua_.PushNil();
                lua_.SetGlobal("mixins");
                lua_.PushNil();
                lua_.SetGlobal("mixinOptions");
                try
                {
                    lua_.DoFile(file);
                }
                catch (LuaException)
                {
                    return;
                }
                lua_.GetGlobal("applyMixins");
                if (!lua_.IsFunction(-1)) return;
                lua_.GetGlobal("mixins");
                if (lua_.IsNil(-1)) return;
                lua_.PushObject(m);
                lua_.Insert(-2);
                lua_.GetGlobal("mixinOptions");
                lua_.Call(3, 0);
            }
            catch (LuaException e)
            {
                ScriptError("luautils::applyMixins", e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnMobSpawnCheck: the script's onMobSpawnCheck(mob); a number other than 0 holds the spawn back (no
        // function, an error or anything else is 0)
        internal int OnMobSpawnCheck(MobEntity m)
        {
            if (lua_ == null) return 0;
            int top = lua_.Top;
            try
            {
                PushMobFunction(m, "onMobSpawnCheck");
                if (!lua_.IsFunction(-1)) return 0;
                lua_.PushObject(m);
                lua_.Call(1, 1);
                return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : 0;
            }
            catch (LuaException e)
            {
                ScriptError("luautils::onMobSpawnCheck", e);
                return 0;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnMobSpawn
        private void OnMobSpawn(MobEntity m)
        {
            TriggerListener(m, "PRESPAWN", m);
            CallMobHook(m, "onMobSpawn", m);
            TriggerListener(m, "SPAWN", m);
        }

        // luautils::OnMobEngage: a char target's next event comes from the mob
        private void OnMobEngage(MobEntity m, BattleEntity target)
        {
            if (target == null) return;
            if (target is CharData c)
            {
                c.EventPreparation.TargetEntity = m;
                c.EventPreparation.ScriptFile = MobScriptFile(m);
            }
            CallMobHook(m, "onMobEngage", m, target);
        }

        // luautils::OnMobDisengage, with the zone's weather
        private void OnMobDisengage(MobEntity m) => CallMobHook(m, "onMobDisengage", m, AreaOf(m).Weather);

        private void OnMobFight(MobEntity m, BattleEntity target)
        {
            if (target != null) CallMobHook(m, "onMobFight", m, target);
        }

        private void OnMobRoam(MobEntity m) => CallMobHook(m, "onMobRoam", m);

        private void OnMobDespawn(MobEntity m) => CallMobHook(m, "onMobDespawn", m);

        // luautils::OnMobDeath: with a killer, xi.mob.onMobDeathEx for each alliance member in the zone, then
        // InteractionGlobal.onMobDeath (the quests' and missions' handlers, then the script's onMobDeath) for each,
        // with the shared optParams, and the killer's DEFEATED_MOB listeners; without one, InteractionGlobal.onMobDeath
        // with no player
        private void OnMobDeath(MobEntity m, CharData killer)
        {
            if (lua_ == null) return;
            string file = MobScriptFile(m);
            int top = lua_.Top;
            LuaRef optRef = null;
            try
            {
                lua_.NewTable();
                int opt = lua_.Top;
                SetField(opt, "isKiller", false);
                SetField(opt, "noKiller", true);
                SetField(opt, "isWeaponSkillKill", false);

                if (killer == null)
                {
                    CallDeathFramework(m, null, opt, file);
                    return;
                }

                lua_.GetPath("xi.mob.onMobDeathEx");
                int ex = lua_.Top;
                var members = new List<CharData>();
                foreach (var member in ForAlliance(killer))
                    if (SameArea(member, killer)) members.Add(member);
                foreach (var member in lua_.IsFunction(ex) ? members : new List<CharData>())
                {
                    m.LocalVars.TryGetValue("weaponskillHit", out uint wsVar);
                    try
                    {
                        lua_.PushValue(ex);
                        lua_.PushObject(m);
                        lua_.PushObject(member);
                        lua_.Push(member == killer);
                        lua_.Push((wsVar & 0xFFFFFF) > 0);
                        lua_.Call(4, 0);
                    }
                    catch (LuaException e)
                    {
                        ScriptError("luautils::onMobDeathEx", e);
                    }
                    lua_.SetTop(ex);
                }

                lua_.PushValue(opt);
                optRef = lua_.Ref();
                foreach (var member in members)
                {
                    m.LocalVars.TryGetValue("weaponskillHit", out uint wsVar);
                    uint wsUsed = wsVar >> 24, wsDamage = wsVar & 0xFFFFFF;
                    SetField(opt, "isKiller", member == killer);
                    SetField(opt, "noKiller", false);
                    SetField(opt, "isWeaponSkillKill", wsUsed > 0);
                    SetField(opt, "weaponskillUsed", wsUsed);
                    SetField(opt, "weaponskillDamage", wsDamage);
                    killer.EventPreparation.TargetEntity = m;
                    killer.EventPreparation.ScriptFile = file;
                    CallDeathFramework(m, member, opt, file);
                    TriggerListener(killer, "DEFEATED_MOB", m, member, optRef);
                }
            }
            finally
            {
                if (optRef != null) lua_.Unref(optRef);
                lua_.SetTop(top);
            }
        }

        // InteractionGlobal.onMobDeath(mob, player, optParams, the script's onMobDeath)
        private void CallDeathFramework(MobEntity m, CharData player, int opt, string file)
        {
            int top = lua_.Top;
            try
            {
                lua_.GetPath("InteractionGlobal.onMobDeath");
                lua_.PushObject(m);
                lua_.PushObject(player);
                lua_.PushValue(opt);
                PushMobFunction(m, "onMobDeath");
                lua_.Call(4, 0);
            }
            catch (LuaException e)
            {
                ScriptError("luautils::onMobDeath", e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        private void SetField(int table, string key, object value)
        {
            lua_.Push(value);
            lua_.SetField(table, key);
        }

        // ---- bindings ---------------------------------------------------------------------------------------------

        private void RegisterMobScriptBindings(Dictionary<string, LuaFunction> m)
        {
            lua_.SetGlobal("GetMobByID", l =>
            {
                uint id = l.ToUIntArg(1);
                // GetMobByID(id, instance): the run's mob or pet at that targid
                var run = InstanceArg(l, 2);
                object mob = run != null ? InstanceEntity(run, (ushort)(id & 0xFFF), (byte)(TypeMob | TypePet)) : MobById(id);
                if (mob == null) log_($"luautils::GetMobByID Mob doesn't exist ({id})");
                l.PushObject(mob);
                return 1;
            });
            lua_.SetGlobal("GetEntityByID", l =>
            {
                uint id = l.ToUIntArg(1);
                bool silence = l.Type(3) == LuaNative.LUA_TBOOLEAN && l.ToBoolean(3);
                if (InstanceArg(l, 2) is { } run)
                {
                    object found = InstanceEntity(run, (ushort)(id & 0xFFF), (byte)(TypeMob | TypePet | TypeNpc));
                    if (found == null && !silence) log_($"luautils::GetEntityByID Mob doesn't exist ({id})");
                    l.PushObject(found);
                    return 1;
                }
                object e = MobById(id);
                if (e == null && HeldZone((ushort)(id >> 12 & 0xFFF))?.NpcById.TryGetValue(id, out var npc) == true) e = npc;
                if (e == null)
                    foreach (var c in charSessions_.Keys)
                        if (c.Id == id) e = c;
                if (e == null && !silence) log_($"luautils::GetEntityByID Entity doesn't exist ({id})");
                l.PushObject(e);
                return 1;
            });

            m["addListener"] = l =>
            {
                if (!l.IsFunction(4)) throw new LuaArgumentException(4, "function", l.Describe(4));
                string eventName = l.ToStringArg(2), identifier = l.ToStringArg(3);
                l.PushValue(4);
                EventsOf(Entity(l)).AddListener(l, eventName, l.Ref(), identifier);
                return 0;
            };
            m["removeListener"] = l =>
            {
                EventsOf(Entity(l)).RemoveListener(l, l.ToStringArg(2));
                return 0;
            };
            m["triggerListener"] = l =>
            {
                int n = l.Top;
                EventsOf(Entity(l)).Trigger(l, l.ToStringArg(2), () =>
                {
                    for (int i = 3; i <= n; i++) l.PushValue(i);
                    return Math.Max(0, n - 2);
                }, msg => log_("lua: " + msg));
                return 0;
            };
            m["addImmunity"] = l =>
            {
                if (Entity(l) is MobEntity mob) mob.Immunity |= (uint)l.ToEnumArg(2); // xi::Immunity
                return 0;
            };
            m["delImmunity"] = l =>
            {
                if (Entity(l) is MobEntity mob) mob.Immunity &= ~(uint)l.ToEnumArg(2);
                return 0;
            };
            m["hasImmunity"] = l =>
            {
                // only a mob (or a pet) is ever immune
                if (Battle(l) is not { } b) return Ret(l, false);
                return Ret(l, b is MobEntity mob && (mob.Immunity & (uint)l.ToEnumArg(2)) != 0);
            };
            m["setRespawnTime"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to set respawn time for invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                uint seconds = l.ToUIntArg(2);
                var spawner = AreaOf(mob).Spawner;
                if (seconds == 0)
                {
                    mob.RespawnTimeSec = 0;
                    mob.AllowRespawn = false;
                    spawner?.Unregister(mob);
                    return 0;
                }
                mob.RespawnTimeSec = seconds;
                mob.AllowRespawn = true;
                if (!mob.IsSpawned) spawner?.RegisterForRespawn(mob, seconds, NowMs());
                return 0;
            };
            m["getRespawnTime"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to get respawn time for invalid entity type ({NameOf(Entity(l))}).");
                    return Ret(l, 0);
                }
                long? remaining = AreaOf(mob).Spawner?.RemainingRespawnMs(mob, NowMs());
                return Ret(l, remaining is long ms ? (uint)(ms / 1000) : 0);
            };
            m["setAnimationSub"] = l =>
            {
                byte sub = (byte)l.ToUIntArg(2);
                bool send = l.IsNil(3) || l.ToBoolean(3);
                switch (Entity(l))
                {
                    case MobEntity mob when mob.AnimationSub != sub:
                        mob.AnimationSub = sub;
                        if (send) BroadcastMobUpdate(mob, EntityUpdatePacket.UpdateCombat);
                        break;
                    case NpcEntity npc when npc.AnimationSub != sub:
                        npc.AnimationSub = sub;
                        if (send) BroadcastNpcUpdate(npc, EntityUpdatePacket.UpdateCombat);
                        break;
                    // a char hears of it on its own 0x037
                    case CharData c when c.AnimationSub != sub:
                        c.AnimationSub = sub;
                        if (send) PushTo(c, CharStatus(c));
                        break;
                }
                return 0;
            };
            m["getAnimationSub"] = l => Entity(l) switch
            {
                MobEntity mob => Ret(l, mob.AnimationSub),
                NpcEntity npc => Ret(l, npc.AnimationSub),
                CharData c => Ret(l, c.AnimationSub),
                _ => Ret(l, 0),
            };
            m["setMagicCastingEnabled"] = l => SetControllerFlag(l, (mob, on) => mob.MagicCastingEnabled = on);
            m["setAutoAttackEnabled"] = l => SetControllerFlag(l, (mob, on) => mob.AutoAttackEnabled = on);
            m["setMobAbilityEnabled"] = l => SetControllerFlag(l, (mob, on) => mob.MobAbilityEnabled = on);
            m["setRangedAttackEnabled"] = l => SetControllerFlag(l, (mob, on) => mob.RangedAttackEnabled = on);
            m["isRangedAttackEnabled"] = l =>
            {
                if (Entity(l) is MobEntity mob) return Ret(l, mob.RangedAttackEnabled);
                log_($"function call on invalid entity! (name: {NameOf(Entity(l))} type: {ObjTypeOf(Entity(l))})");
                return Ret(l, false);
            };
            // CBattleEntity::isAlive: !isDead, so a despawned (Disappear) mob, or one in its death or despawn state, is not
            m["isAlive"] = l =>
            {
                if (Entity(l) is BattleEntity b) return Ret(l, b is MobEntity ? !StatusEffectContainer.IsDead(b) : b.IsAlive);
                log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                return Ret(l, false);
            };
            // CBattleEntity::isDead: a mob never spawned or despawned (Disappear) counts as dead too
            m["isDead"] = l =>
            {
                if (Entity(l) is BattleEntity b) return Ret(l, b is MobEntity ? StatusEffectContainer.IsDead(b) : b.Hp == 0);
                log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                return Ret(l, false);
            };
            m["isSpawned"] = l =>
            {
                switch (Entity(l))
                {
                    case MobEntity mob: return Ret(l, mob.IsSpawned);
                    case NpcEntity npc: return Ret(l, npc.Status != NpcEntity.StatusDisappear);
                }
                log_("CLuaBaseEntity::isSpawned() called on entity that is not a CMobEntity or CNpcEntity.");
                return Ret(l, false);
            };
            m["isEngaged"] = l => Ret(l, Entity(l) switch
            {
                MobEntity mob => mob.Engaged,
                CharData c => c.BattleTarget != null,
                _ => false,
            });
            m["getPos"] = l =>
            {
                var (x, y, z, rot) = PosOf(Entity(l));
                PushPos(l, x, y, z, rot);
                return 1;
            };
            // CLuaBaseEntity::getMaster: the linked master (an NPC self warns and returns nil, as the C++ TYPE_NPC
            // check): setPet's link, a charm's char (ZoneHost.Charm.cs), a player pet's or a trust's master.
            m["getMaster"] = l =>
            {
                if (Entity(l) is NpcEntity npc) { log_($"Invalid Entity (NPC: {npc.Name}) calling function."); l.PushNil(); return 1; }
                l.PushObject(((BattleEntity)Entity(l)).PMaster);
                return 1;
            };
            m["getSpawnPos"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Calling entity is not a Mob ({NameOf(Entity(l))}).");
                    l.PushNil();
                    return 1;
                }
                PushPos(l, mob.SpawnX, mob.SpawnY, mob.SpawnZ, mob.SpawnRotation);
                return 1;
            };
        }

        private int SetControllerFlag(LuaState l, Action<MobEntity, bool> set)
        {
            if (Entity(l) is MobEntity mob) set(mob, l.ToBooleanArg(2));
            else log_($"function call on invalid entity! (name: {NameOf(Entity(l))} type: {ObjTypeOf(Entity(l))})");
            return 0;
        }

        private static (float X, float Y, float Z, byte Rot) PosOf(object e) => e switch
        {
            BattleEntity b => (b.X, b.Y, b.Z, b.Rotation),
            NpcEntity n => (n.X, n.Y, n.Z, n.Rotation),
            _ => (0, 0, 0, 0),
        };

        private static void PushPos(LuaState l, float x, float y, float z, byte rot)
        {
            l.NewTable(0, 4);
            l.Push(x);
            l.SetField(-2, "x");
            l.Push(y);
            l.SetField(-2, "y");
            l.Push(z);
            l.SetField(-2, "z");
            l.Push(rot);
            l.SetField(-2, "rot");
        }

        // zoneutils::GetEntity for a mob: its zone loaded if it is not yet, none in another process's zone
        private MobEntity MobById(uint id) => HeldZone((ushort)(id >> 12 & 0xFFF))?.MobById.TryGetValue(id, out var mob) == true ? mob : null;

        // CZone::UpdateEntityPacket for a mob: every char that has it spawned
        private void BroadcastMobUpdate(MobEntity m, byte updatemask)
        {
            foreach (var (c, s) in charSessions_)
                if (SameArea(c, m) && c.SpawnMobList.Contains(m.Id)) UpdateEntityPacket(s, m, EntityUpdate.Update, updatemask);
        }
    }
}
