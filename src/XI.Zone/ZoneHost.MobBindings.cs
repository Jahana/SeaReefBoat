using System;
using System.Collections.Generic;
using System.Text;
using XI.Map;
using XI.Script;

namespace XI.Zone
{
    // The CLuaBaseEntity bindings mob scripts call most (M6b): CAIContainer's action queue behind timer() and
    // queue(), behavior, TP / HP / MP, unkillable, untargetable and hidden names, status, the target and the current
    // action, enmity and the claim, spawning and despawning (the SpawnMob / DespawnMob globals too) and 0x038
    // entity animations (getInstance is in ZoneHost.Instances.cs, getBattlefield in ZoneHost.Battlefield.cs). A char's
    // setBaseSpeed (bind's) sets its base speed; hideName, setStatus and setAnimationSub stay unported for a char,
    // whose namevis, status and animationsub are not modeled.
    public sealed partial class ZoneHost
    {
        private readonly Dictionary<object, AiActionQueue> actionQueues_ = new Dictionary<object, AiActionQueue>(ReferenceEqualityComparer.Instance);

        // CZoneEntities::ZoneServer's entity order, zone by zone (an instance run is a zone of its own): the mobs, then
        // the NPCs, the pets, the trusts and last the chars, each list a std::map by targid
        private static int TickKind(object e) => e switch
        {
            TrustEntity => 3,
            PetEntity => 2,
            MobEntity => 0,
            NpcEntity => 1,
            _ => 4,
        };

        private static ushort TickTargId(object e) => e switch
        {
            BattleEntity b => b.TargId,
            NpcEntity n => n.TargId,
            _ => 0,
        };

        // CAIContainer::Tick's ActionQueue.checkAction for every entity with something queued, in
        // CZoneEntities::ZoneServer's order (TickKind, then targid) zone by zone; a char's TICK listeners run right
        // before its queue. A char gone from its zone (SendToZone: the C++ removes it from the zone's char list at the
        // end of that tick) no longer ticks. The deviations left: TODO.md's tick order row (ZoneHost.Listeners.cs)
        private void TickEntityActionQueues()
        {
            if (lua_ == null) return;
            long now = NowMs();
            var areas = AllAreas();
            var areaIndex = new Dictionary<ZoneInfo, int>(ReferenceEqualityComparer.Instance);
            for (int i = 0; i < areas.Count; i++) areaIndex[areas[i].Info] = i;
            int AreaRank(object e) =>
                LoadedAreaOf(e) is { } info && areaIndex.TryGetValue(info, out int i) ? i : -1;

            // per area: the queued non-chars, then the chars; the order they were added in breaks the last ties
            // (List.Sort is not stable)
            var work = new List<(int Area, int Kind, ushort TargId, int Added, object Entity)>();
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            void Add(object e)
            {
                int area = AreaRank(e);
                if (area >= 0 && seen.Add(e)) work.Add((area, TickKind(e), TickTargId(e), work.Count, e));
            }
            foreach (var entity in actionQueues_.Keys)
                if (entity is not CharData) Add(entity);
            foreach (var c in charSessions_.Keys)
                if (!c.ZonedOut) Add(c);
            work.Sort((a, b) => a.Area != b.Area ? a.Area.CompareTo(b.Area)
                              : a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind)
                              : a.TargId != b.TargId ? a.TargId.CompareTo(b.TargId) : a.Added.CompareTo(b.Added));

            foreach (var (_, _, _, _, entity) in work)
            {
                if (entity is CharData c)
                {
                    if (!charSessions_.ContainsKey(c) || c.ZonedOut) continue;
                    TickCharListeners(c, now);
                }
                if (actionQueues_.TryGetValue(entity, out var queue)) CheckActions(entity, queue, now);
            }

            // what is in no area of AllAreas (an instance run already gone, a zone not loaded): the non-chars' queues
            // in insertion order, then such a char's TICK listeners and queue (as every char's ran before the C++
            // order), then the queue of a char with no session left (none should be: ClearActionQueues runs at each
            // teardown)
            foreach (var (entity, queue) in new List<KeyValuePair<object, AiActionQueue>>(actionQueues_))
                if (entity is not CharData && !seen.Contains(entity)) CheckActions(entity, queue, now);
            foreach (var c in new List<CharData>(charSessions_.Keys))
            {
                if (seen.Contains(c) || c.ZonedOut || !charSessions_.ContainsKey(c)) continue;
                TickCharListeners(c, now);
                if (actionQueues_.TryGetValue(c, out var queue)) CheckActions(c, queue, now);
            }
            if (actionQueues_.Count == 0) return;
            foreach (var (entity, queue) in new List<KeyValuePair<object, AiActionQueue>>(actionQueues_))
                if (entity is CharData c && !charSessions_.ContainsKey(c) && !c.ZonedOut)
                    CheckActions(entity, queue, now);
        }

        // AreaOf without loading a zone (Zone() loads one it does not hold): the entity's instance run, else its zone
        // when this process holds it; null for anything else
        private ZoneInfo LoadedAreaOf(object e)
        {
            ushort zone;
            Instance run;
            switch (e)
            {
                case BattleEntity b: zone = b.Zone; run = b.Instance; break;
                case NpcEntity n: zone = ZoneOf(n); run = n.Instance; break;
                default: return null;
            }
            if (EffectiveInstance(run, zone) is { } r) return r.Info;
            return zones_.TryGetValue(zone, out var z) ? z.Info : null;
        }

        // CAIActionQueue::checkAction: every action due (and, for a queue action, the entity able to change state)
        private void CheckActions(object entity, AiActionQueue queue, long now)
        {
            while (queue.TryPopDue(now, () => CanChangeState(entity), out var func, out var native))
            {
                if (native != null) native(entity);
                else RunQueuedAction(entity, func);
            }
            if (queue.IsEmpty) actionQueues_.Remove(entity);
        }

        // CAIContainer::CanChangeState: not while dying (a mob's death, a char's death state), nor under a state that
        // holds it (a mob skill, a cast, a WS / ranged / ability / item state, inactive)
        private static bool CanChangeState(object entity) => entity switch
        {
            MobEntity m => (!m.IsSpawned || !m.IsDead) && m.SkillState == null && m.MagicState == null && m.RangedState == null &&
                           m.InactiveState is null or { CanChangeState: true } && m is not TrustEntity { WsState: not null } &&
                           m.AbilityState is null or { Completed: true },
            CharData c => c.Hp > 0 && c.WsState == null && c.SkillState == null && c.RangedState == null && c.AbilityState is null or { Completed: true } &&
                          c.MagicState == null && c.ItemUse == null && c.InactiveState is null or { CanChangeState: true },
            _ => true,
        };

        // wrapLuaAction: the function called with the entity
        private void RunQueuedAction(object entity, LuaRef func)
        {
            int top = lua_.Top;
            try
            {
                lua_.PushRef(func);
                lua_.Push(entity);
                lua_.Call(1, 0);
            }
            catch (LuaException e)
            {
                log_($"CAIActionQueue Lua action for {NameOf(entity)} ({IdOf(entity)}): {e.Message}");
            }
            finally
            {
                lua_.SetTop(top);
                lua_.Unref(func);
            }
        }

        private void QueueAction(LuaState l, bool checkState)
        {
            if (!l.IsFunction(3)) throw new LuaArgumentException(3, "function", l.Describe(3));
            object entity = Entity(l);
            long deadline = NowMs() + l.ToIntArg(2);
            l.PushValue(3);
            var func = l.Ref();
            if (!actionQueues_.TryGetValue(entity, out var queue)) actionQueues_[entity] = queue = new AiActionQueue();
            queue.Push(deadline, checkState, func);
        }

        private void QueueNative(object entity, long delayMs, bool checkState, Action<object> action)
        {
            if (!actionQueues_.TryGetValue(entity, out var queue)) actionQueues_[entity] = queue = new AiActionQueue();
            queue.Push(NowMs() + delayMs, checkState, action);
        }

        private void ClearActionQueues(object entity)
        {
            if (!actionQueues_.TryGetValue(entity, out var queue)) return;
            queue.ClearActions(lua_);
            queue.ClearTimers(lua_);
            actionQueues_.Remove(entity);
        }

        // the entity update a status / flag / name change sends: a mob's and a char's on their next tick, an NPC's now
        private void MarkUpdate(object e, byte updatemask)
        {
            switch (e)
            {
                case BattleEntity b: b.UpdateMask |= updatemask; break;
                case NpcEntity n when n.Status != NpcEntity.StatusDisappear: BroadcastNpcUpdate(n, updatemask); break;
            }
        }

        // CZone::PushPacket(CHAR_INRANGE) for a non-char: the chars in the zone within 50 yalms
        private void PushCharsNear(object e, BasicPacket p)
        {
            var (x, y, z, _) = PosOf(e);
            foreach (var (c, s) in charSessions_)
            {
                if (!SameArea(c, e)) continue;
                float dx = c.X - x, dy = c.Y - y, dz = c.Z - z;
                if (dx * dx + dy * dy + dz * dz <= 50 * 50) s.PacketList.AddLast(new BasicPacket(p.Buffer));
            }
        }

        private static ushort TargIdOf(object e) => e is BattleEntity b ? b.TargId : ((NpcEntity)e).TargId;

        // GP_SERV_COMMAND_SCHEDULOR (0x038): an animation by its four-character id, from an entity at a target
        private static BasicPacket Schedulor(object entity, object target, string command)
        {
            var id = new byte[4];
            Encoding.ASCII.GetBytes(command, 0, Math.Min(command.Length, 4), id, 0);
            var w = new PacketWriter(0x038, 20);
            w.U32(4, IdOf(entity));
            w.U32(8, IdOf(target));
            w.U32(12, BitConverter.ToUInt32(id, 0));
            w.U16(16, TargIdOf(entity));
            w.U16(18, TargIdOf(target));
            return w.Packet;
        }

        // luautils::SpawnMob / CLuaBaseEntity::spawn: a despawn timer, then either a respawn time (the mob is left
        // down) or the spawn itself
        private void ScriptSpawn(MobEntity m, double? despawnSec, double? respawnSec)
        {
            // the seconds are as<uint32> (LuaState.LlRound)
            if (despawnSec is double d) m.DespawnAtMs = (uint)LuaState.LlRound(d) is uint ds and > 0 ? NowMs() + ds * 1000L : 0;
            if (respawnSec is double r)
            {
                m.RespawnTimeSec = (uint)LuaState.LlRound(r);
                m.AllowRespawn = true;
            }
            else if (!m.IsSpawned) SpawnMob(AreaOf(m), m);
            else log_($"SpawnMob: {m.Id} <{m.Name}> is already spawned");
        }

        private void RegisterMobAiBindings(Dictionary<string, LuaFunction> m)
        {
            lua_.SetGlobal("SpawnMob", l =>
            {
                uint id = l.ToUIntArg(1);
                // SpawnMob(id, instance): the run's mob at that targid
                var run = InstanceArg(l, 2);
                var mob = run != null ? InstanceEntity(run, (ushort)(id & 0xFFF), TypeMob) as MobEntity : MobById(id);
                if (mob == null)
                {
                    log_($"SpawnMob: mob <{id}> not found");
                    l.PushNil();
                    return 1;
                }
                ScriptSpawn(mob, run != null ? null : l.OptNumber(2), l.OptNumber(3));
                l.PushObject(mob);
                return 1;
            });
            lua_.SetGlobal("DespawnMob", l =>
            {
                var run = InstanceArg(l, 2);
                uint id = l.ToUIntArg(1);
                if ((run != null ? InstanceEntity(run, (ushort)(id & 0xFFF), TypeMob) as MobEntity : MobById(id)) is not { } mob) return 0;
                if (l.OptNumber(2) is double sec) mob.DespawnAtMs = (uint)LuaState.LlRound(sec) is uint ds and > 0 ? NowMs() + ds * 1000L : 0;
                else if (mob.IsSpawned && mob.FadeAtMs == 0) DespawnMob(AreaOf(mob), mob, NowMs());
                return 0;
            });

            m["timer"] = l => { QueueAction(l, false); return 0; };
            m["queue"] = l => { QueueAction(l, true); return 0; };
            m["clearActionQueue"] = l =>
            {
                if (actionQueues_.TryGetValue(Entity(l), out var queue)) queue.ClearActions(l);
                return 0;
            };
            m["clearTimerQueue"] = l =>
            {
                if (actionQueues_.TryGetValue(Entity(l), out var queue)) queue.ClearTimers(l);
                return 0;
            };
            m["spawn"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to spawn invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                ScriptSpawn(mob, l.IsNil(2) ? null : l.ToNumberArg(2), l.IsNil(3) ? null : l.ToNumberArg(3));
                return 0;
            };
            m["setSpawn"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to set spawn for invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                mob.SpawnX = (float)l.ToNumberArg(2);
                mob.SpawnY = (float)l.ToNumberArg(3);
                mob.SpawnZ = (float)l.ToNumberArg(4);
                mob.SpawnRotation = l.IsNil(5) ? (byte)0 : (byte)l.ToIntArg(5);
                return 0;
            };

            m["getBehavior"] = l =>
            {
                if (Entity(l) is MobEntity mob) return Ret(l, mob.Behavior);
                log_($"Attempting to get behavior for invalid entity type ({NameOf(Entity(l))}).");
                return Ret(l, 0);
            };
            m["setBehavior"] = l =>
            {
                if (Entity(l) is MobEntity mob) mob.Behavior = (ushort)l.ToEnumArg(2); // xi::Behavior
                else log_($"Attempting to set behavior for invalid entity type ({NameOf(Entity(l))}).");
                return 0;
            };
            m["setDelay"] = l =>
            {
                if (Mob(l) is { } mob) mob.MainDelay = (ushort)l.ToIntArg(2);
                return 0;
            };
            m["getBattleTime"] = l => Battle(l) is { } b ? Ret(l, (uint)((NowMs() - b.BattleStartMs) / 1000)) : Ret(l, 0);
            m["isNM"] = l => Ret(l, Entity(l) is MobEntity mob && mob.IsNotorious);
            m["isUndead"] = l => Ret(l, Battle(l) is MobEntity mob && mob.Ecosystem == Ecosystem.Undead);
            m["getHP"] = l => Ret(l, Battle(l) is { } b ? b.Hp : 0);
            m["getHPP"] = l => Ret(l, Battle(l) is { } b ? b.Hpp : 0);
            m["getMaxHP"] = l => Ret(l, Battle(l) is { } b ? b.MaxHp : 0);
            m["getMP"] = l => Ret(l, Battle(l) is { } b ? b.Mp : 0);
            m["getMPP"] = l => Ret(l, Battle(l) is { } b ? b.Mpp : 0);
            m["getMaxMP"] = l => Ret(l, Battle(l) is { } b ? b.MaxMp : 0);
            m["getTP"] = l => Ret(l, Battle(l) is { } b ? (float)b.Tp : 0);
            // addHP always wakes the entity; addHPLeaveSleeping does not
            m["addHP"] = l =>
            {
                if (Battle(l) is not { } b) return Ret(l, 0);
                int result = BattleAddHp(b, l.ToIntArg(2));
                (b as CharData)?.StatusEffects.WakeUp();
                return Ret(l, result);
            };
            m["addHPLeaveSleeping"] = l => Ret(l, Battle(l) is { } b ? BattleAddHp(b, l.ToIntArg(2)) : 0);
            m["restoreHP"] = l => Ret(l, Battle(l) is { } b && b.Animation != Anim.Death ? BattleAddHp(b, l.ToIntArg(2)) : 0);
            m["addMP"] = l => Ret(l, Battle(l) is { } b ? BattleAddMp(b, l.ToIntArg(2)) : 0);
            m["delHP"] = l =>
            {
                if (Battle(l) is { } b) BattleTakeDamage(b, l.ToIntArg(2), null, 0, 0);
                return 0;
            };
            m["takeDamage"] = TakeDamageBinding;

            m["setTP"] = l =>
            {
                if (Entity(l) is BattleEntity b)
                {
                    b.Tp = (short)l.ToIntArg(2);
                    b.UpdateMask |= EntityUpdatePacket.UpdateHp;
                }
                return 0;
            };
            m["setHP"] = l =>
            {
                if (Battle(l) is not { } b) return 0;
                int value = l.ToIntArg(2);
                b.Hp = 0;
                BattleAddHp(b, value);
                b.UpdateMask |= EntityUpdatePacket.UpdateHp;
                // at 0 the entity falls, so the last attacker goes
                if (value == 0)
                {
                    if (b is CharData pc) pc.LastAttackerId = 0;
                    else ((MobEntity)b).LastAttackerId = 0;
                }
                return 0;
            };
            m["setMP"] = l =>
            {
                if (Battle(l) is not { } b) return 0;
                int value = l.ToIntArg(2);
                b.Mp = 0;
                BattleAddMp(b, value);
                return 0;
            };
            m["setMobLevel"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to set mob level for invalid entity type ({NameOf(Entity(l))}, {l.ToIntArg(2)}).");
                    return 0;
                }
                mob.MLvl = mob.SLvl = (byte)l.ToIntArg(2);
                // Remove traits, because they were calculated in the previous CalculateMobStats and are NOT saved
                mob.TraitList.Clear();
                MobStatsFor().CalculateMobStats(mob, l.IsNil(3) || l.ToBoolean(3), AreaOf(mob).Types.Contains("instanced"));
                return 0;
            };
            m["setUnkillable"] = l =>
            {
                if (Entity(l) is BattleEntity b) b.Unkillable = l.ToBooleanArg(2);
                return 0;
            };
            m["getUnkillable"] = l => Ret(l, Entity(l) is BattleEntity b && b.Unkillable);
            m["setUntargetable"] = l =>
            {
                bool on = l.ToBooleanArg(2);
                switch (Entity(l))
                {
                    case MobEntity mob: mob.Untargetable = on; break;
                    case NpcEntity npc:
                        npc.EntityFlags = on ? npc.EntityFlags | EntityFlagUntargetable : npc.EntityFlags & ~EntityFlagUntargetable;
                        break;
                    default:
                        log_($"Attempt to set untargetable for invalid entity ({NameOf(Entity(l))})");
                        return 0;
                }
                MarkUpdate(Entity(l), EntityUpdatePacket.UpdateHp);
                return 0;
            };
            m["getUntargetable"] = l => Ret(l, Entity(l) switch
            {
                MobEntity mob => mob.Untargetable,
                NpcEntity npc => (npc.EntityFlags & EntityFlagUntargetable) != 0,
                _ => false,
            });
            m["hideName"] = l =>
            {
                bool hide = l.ToBooleanArg(2);
                switch (Entity(l))
                {
                    case MobEntity mob: mob.HideName(hide); break;
                    case NpcEntity npc:
                        npc.NameVis = hide ? (byte)(npc.NameVis | MobEntity.NameVisHideName) : (byte)(npc.NameVis & ~MobEntity.NameVisHideName);
                        MarkUpdate(npc, EntityUpdatePacket.UpdateHp);
                        break;
                    // CBaseEntity::HideName: the namevis bit, then UPDATE_HP
                    case CharData c:
                        c.NameVis = hide ? (byte)(c.NameVis | MobEntity.NameVisHideName) : (byte)(c.NameVis & ~MobEntity.NameVisHideName);
                        c.UpdateMask |= CharStats.UpdateHp;
                        break;
                }
                return 0;
            };
            m["isNameHidden"] = l => Ret(l, Entity(l) switch
            {
                MobEntity mob => (mob.NameVis & MobEntity.NameVisHideName) != 0,
                NpcEntity npc => (npc.NameVis & MobEntity.NameVisHideName) != 0,
                CharData c => (c.NameVis & MobEntity.NameVisHideName) != 0,
                _ => false,
            });
            m["getStatus"] = l => Ret(l, Entity(l) switch
            {
                MobEntity mob => mob.Status,
                NpcEntity npc => npc.Status,
                CharData c => c.Status,
                _ => NpcEntity.StatusNormal,
            });
            m["setStatus"] = l =>
            {
                byte status = (byte)l.ToEnumArg(2); // xi::Status
                switch (Entity(l))
                {
                    case MobEntity mob: mob.Status = status; break;
                    case NpcEntity npc: npc.Status = status; break;
                    case CharData c: c.Status = status; break;
                }
                MarkUpdate(Entity(l), EntityUpdatePacket.UpdateHp);
                return 0;
            };
            m["getRotPos"] = l => Ret(l, PosOf(Entity(l)).Rot);
            m["setRotation"] = l =>
            {
                byte rot = (byte)l.ToIntArg(2);
                switch (Entity(l))
                {
                    case BattleEntity b: b.Rotation = rot; break;
                    case NpcEntity npc: npc.Rotation = rot; break;
                }
                MarkUpdate(Entity(l), EntityUpdatePacket.UpdatePos);
                return 0;
            };
            m["lookAt"] = l =>
            {
                float px, py, pz;
                if (l.IsNumber(2))
                {
                    px = (float)l.ToNumber(2);
                    py = (float)l.ToNumber(3);
                    pz = (float)l.ToNumber(4);
                }
                else
                {
                    px = TableFloat(l, 2, "x");
                    py = TableFloat(l, 2, "y");
                    pz = TableFloat(l, 2, "z");
                }
                var (x, y, z, rot) = PosOf(Entity(l));
                float dx = px - x, dy = py - y, dz = pz - z;
                if (dx * dx + dy * dy + dz * dz <= 0.1f * 0.1f) return 0;
                byte angle = WorldAngle(x, z, rot, px, pz);
                switch (Entity(l))
                {
                    case BattleEntity b: b.Rotation = angle; break;
                    case NpcEntity npc: npc.Rotation = angle; break;
                }
                MarkUpdate(Entity(l), EntityUpdatePacket.UpdatePos);
                return 0;
            };
            m["setBaseSpeed"] = l =>
            {
                byte speed = (byte)Math.Min(l.ToIntArg(2), 255);
                // an NPC's speed is its base speed (CBaseEntity::UpdateSpeed)
                if (Entity(l) is NpcEntity npc)
                {
                    if (npc.Speed == speed) return 0;
                    npc.Speed = speed;
                    if (npc.Status != NpcEntity.StatusDisappear) BroadcastNpcUpdate(npc, EntityUpdatePacket.UpdatePos);
                    return 0;
                }
                // a char's baseSpeed is BaseSpeedOverride (map.BASE_SPEED while null); the char hears of it on 0x037
                if (Entity(l) is CharData c)
                {
                    if ((c.BaseSpeedOverride ?? MountUtils.BaseSpeed) == speed) return 0;
                    c.BaseSpeedOverride = speed;
                    MountUtils.UpdateSpeed(c);
                    PushTo(c, CharStatus(c));
                    return 0;
                }
                if (Entity(l) is not MobEntity mob) return 0;
                if (mob.BaseSpeed == speed) return 0;
                mob.BaseSpeed = speed;
                MobStats.UpdateSpeed(mob);
                if (mob.IsSpawned) BroadcastMobUpdate(mob, EntityUpdatePacket.UpdatePos);
                return 0;
            };

            m["getTarget"] = l =>
            {
                l.PushObject(Battle(l) switch
                {
                    MobEntity mob => mob.BattleTarget,
                    CharData c => c.BattleTarget,
                    _ => null,
                });
                return 1;
            };
            // the AI state as scripts/enum/action.lua numbers it: 1 attacking, 22 dead, 24 despawning, 27 inactive,
            // 30 casting, 34 a mob skill (a pet skill: its finish category), 16 roaming
            m["getCurrentAction"] = l => Battle(l) switch
            {
                MobEntity mob => Ret(l, mob.FadeAtMs != 0 ? 24 : mob.IsSpawned && mob.IsDead ? 22 :
                    mob.SkillState != null ? (mob.SkillState.Skill.IsPetSkill ? mob.SkillState.Skill.FinishCategory : 34) :
                    mob.MagicState != null ? 30 : mob.InactiveState != null ? ActionInactive : mob.Engaged ? 1 : 16),
                CharData c => Ret(l, c.DeathAtMs != 0 ? 22 : c.MagicState != null ? 30 : c.InactiveState != null ? ActionInactive : c.AttackTarget != null ? 1 : 16),
                _ => Ret(l, 0),
            };
            // CAIContainer::Disengage: a mob's controller lets go (OnDisengage too when it was attacking), a char's
            // attack state ends on its next tick
            m["disengage"] = l =>
            {
                switch (Entity(l))
                {
                    case MobEntity mob when mob.Engaged: MobDisengage(mob, NowMs()); break;
                    case MobEntity mob: MobControllerDisengage(mob, NowMs()); break;
                    case CharData c: Disengage(c); break;
                    default: log_($"Invalid entity type calling function ({NameOf(Entity(l))})."); break;
                }
                return 0;
            };
            m["canChangeState"] = l => Ret(l, CanChangeState(Entity(l)));

            m["resetEnmity"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to reset enmity for invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                if (l.ToObject(2) is BattleEntity target) mob.Enmity.LowerEnmityByPercent(target, 100, null);
                return 0;
            };
            m["updateEnmity"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to update enmity for invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                // PAI->Engage; only chars are ported as targets
                if (l.ToObject(2) is CharData target && mob.IsAlive && !mob.IsDead) MobEngage(mob, target, NowMs());
                return 0;
            };
            m["updateClaim"] = l =>
            {
                if (Entity(l) is not MobEntity mob) return 0;
                switch (l.ToObject(2))
                {
                    case CharData c: ClaimMob(mob, c, NowMs()); break;
                    case NpcEntity: break;
                    case MobEntity: break;
                    default:
                        mob.OwnerId = 0;
                        mob.UpdateMask |= EntityUpdatePacket.UpdateStatus;
                        break;
                }
                return 0;
            };
            m["entityAnimationPacket"] = l =>
            {
                string command = l.ToStringArg(2);
                object entity = Entity(l), target = l.IsNil(3) ? entity : l.ToObject(3) ?? entity;
                var packet = Schedulor(entity, target, command);
                if (entity is CharData c) PushTo(c, packet);
                else PushCharsNear(entity, packet);
                return 0;
            };
        }

        private const int RoeEventDmgTaken = 5, RoeEventDmgDealt = 6;

        // CBattleEntity::addHP for either kind
        private int BattleAddHp(BattleEntity b, int hp) => b is CharData c ? AddHp(c, hp) : MobAddHp((MobEntity)b, hp);

        // CBattleEntity::takeDamage: the last attacker, TAKE_DAMAGE, RoE's damage taken / dealt, a char's synthesis
        // crit-failed, ABSORB_DMG_TO_MP, then the HP
        private int BattleTakeDamage(BattleEntity defender, int amount, BattleEntity attacker, byte attackType, byte damageType)
        {
            uint lastAttacker = attacker?.Id ?? 0;
            if (defender is CharData dc) dc.LastAttackerId = lastAttacker;
            else
            {
                var dm = (MobEntity)defender;
                dm.LastAttackerId = lastAttacker;
                dm.LastHitAtkType = attackType;
            }
            TriggerListener(defender, "TAKE_DAMAGE", defender, amount, attacker, (int)attackType, (int)damageType);
            if (defender is CharData pc)
            {
                if (amount > 0)
                {
                    RoeEvent(RoeEventDmgTaken, pc, new (string, object)[] { ("dmg", amount) });
                    if (pc.Synth != null && SessionOf(pc) is MapSession s) ForceSynthCritFail("CBattleEntity::takeDamage", s, pc);
                }
            }
            else if (attacker is CharData ac)
            {
                if (amount > 0) RoeEvent(RoeEventDmgDealt, ac, new (string, object)[] { ("dmg", amount) });
                LocalVars(defender)["weaponskillHit"] = 0;
            }
            int absorbMp = defender.GetMod(Mod.ABSORB_DMG_TO_MP);
            if (absorbMp > 0 && (short)(amount * absorbMp / 100) is var mp && mp > 0) BattleAddMp(defender, mp);
            return BattleAddHp(defender, -amount);
        }

        private static int BattleAddMp(BattleEntity b, int mp)
        {
            if (b is CharData c) return AddMp(c, mp);
            int cap = Math.Clamp(b.Mp + mp, 0, b.MaxMp);
            mp = b.Mp - cap;
            b.Mp = cap;
            if (mp != 0) b.UpdateMask |= EntityUpdatePacket.UpdateHp;
            return Math.Abs(mp);
        }

        // CLuaBaseEntity::takeDamage(damage, attacker, attackType, damageType, { wakeUp, removePetrify, breakBind })
        private int TakeDamageBinding(LuaState l)
        {
            if (Battle(l) is not { } defender) return 0;
            int damage = l.ToIntArg(2);
            BattleEntity attacker = null;
            if (!l.IsNil(3))
            {
                if (l.ToObject(3) is not BattleEntity a)
                {
                    log_("CLuaBaseEntity::takeDamage() - PLuaAttacker was null, or was NPC.");
                    return 0;
                }
                attacker = a;
            }
            bool wakeUp = true, breakBind = true, removePetrify = false;
            if (!l.IsNil(6))
            {
                // the flags map: a key left out is false
                wakeUp = TableBool(l, 6, "wakeUp");
                removePetrify = TableBool(l, 6, "removePetrify");
                breakBind = TableBool(l, 6, "breakBind");
            }
            var effects = defender.StatusEffects;
            // nightmare (tier 4 and up) holds through DoT damage; the Diabolos NM's (tier 11) through any damage
            if (effects?.GetStatusEffect(EffectId.SleepI) is { Tier: >= 4 } sleep)
            {
                if (!breakBind) wakeUp = false;
                if (wakeUp && sleep.Tier >= 11) wakeUp = false;
            }
            if (effects?.GetStatusEffect(EffectId.Petrification) is { SubPower: 1 }) removePetrify = true;
            byte attackType = l.IsNil(4) ? (byte)0 : (byte)l.ToIntArg(4);
            byte damageType = l.IsNil(5) ? (byte)0 : (byte)l.ToIntArg(5);
            BattleTakeDamage(defender, damage, attacker, attackType, damageType);
            if (damage > 0 && effects != null)
            {
                if (wakeUp) effects.WakeUp();
                if (removePetrify) effects.DelStatusEffect(EffectId.Petrification);
            }
            // Bind has a chance to break from all direct attacks, even if they don't deal damage
            if (attacker != null && breakBind) BindBreakCheck(defender);
            return 0;
        }

        private static bool TableBool(LuaState l, int idx, string key)
        {
            l.GetField(idx, key);
            bool v = l.ToBoolean(-1);
            l.Pop(1);
            return v;
        }

        private static float TableFloat(LuaState l, int idx, string key)
        {
            l.GetField(idx, key);
            float v = (float)l.ToNumber(-1);
            l.Pop(1);
            return v;
        }
    }
}
