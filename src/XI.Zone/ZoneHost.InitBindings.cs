using System;
using System.Collections.Generic;
using XI.Common;
using XI.Map;
using XI.Script;
using LuaFunction = XI.Script.LuaFunction;

namespace XI.Zone
{
    // Bindings the zones' onInitialize (and the mob / NPC scripts and mixins it reaches) needed that had none, found by
    // XI.Verify.ZoneInit.Live: the luautils globals DisallowRespawn and GetMobRespawnTime, and the CLuaBaseEntity
    // methods setDeathType / getDeathType, hideHP, setAnimationSpeed, setNpcAlwaysRelevant, getAnimation,
    // setAggressive, setRoamFlags, setMobSkillAttack, setCrystalElement, setSpawnAnimation, hideNPC, setMaxMP, updateToEntireZone and
    // addPeriodicTrigger (CTriggerHandler: the NPC's onTimeTrigger every period Vana'diel minutes), and the global
    // InitializeFishingContestSystem.
    public sealed partial class ZoneHost
    {
        private const uint EntityFlagHideHp = 0x100; // xi::EntityFlags::HideHp

        // Trigger_t: an NPC's trigger id, its period and offset (in Vana'diel ticks) and the last period it fired in
        private sealed class TimeTrigger
        {
            public NpcEntity Npc;
            public byte Id;
            public long Period, Offset, LastTrigger;
        }

        private readonly List<TimeTrigger> timeTriggers_ = new List<TimeTrigger>();

        // CTriggerHandler::triggerTimer (the time server's tick): a trigger fires once each time its period rolls over
        private void TickTimeTriggers()
        {
            if (timeTriggers_.Count == 0 || lua_ == null) return;
            long now = VanaTicksNow();
            foreach (var t in timeTriggers_.ToArray())
            {
                long count = (now - t.Offset) / t.Period;
                if (count <= t.LastTrigger) continue;
                t.LastTrigger = count;
                OnTimeTrigger(t.Npc, t.Id);
            }
        }

        // luautils::OnTimeTrigger: the NPC script's onTimeTrigger(npc, triggerID)
        private void OnTimeTrigger(NpcEntity npc, byte id)
        {
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction($"./scripts/zones/{AreaOf(npc).Name}/npcs/{npc.Name}.lua", "onTimeTrigger");
                if (lua_.IsNil(-1)) return;
                lua_.PushObject(npc);
                lua_.Push(id);
                lua_.Call(2, 0);
            }
            catch (LuaException e)
            {
                ScriptError("luautils::onTimeTrigger", e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // hideNPC's despawn: the chars that have the NPC spawned lose it (SpawnNpcs brings it back once it shows)
        private void DespawnNpc(NpcEntity n)
        {
            foreach (var (c, s) in charSessions_)
                if (c.SpawnNpcList.Remove(n.Id)) UpdateEntityPacket(s, n, EntityUpdate.Despawn, 0);
        }

        private void RegisterInitGlobals()
        {
            // luautils::InitializeFishingContestSystem (Selbina's onInitialize): the contest loads now, not on first use
            lua_.SetGlobal("InitializeFishingContestSystem", l =>
            {
                _ = Contest;
                return 0;
            });
            // luautils::DisallowRespawn(mobid, disallow)
            lua_.SetGlobal("DisallowRespawn", l =>
            {
                uint id = l.ToUIntArg(1);
                if (MobById(id) is MobEntity mob) mob.AllowRespawn = !l.ToBoolean(2);
                else log_($"DisallowRespawn: mob <{id}> not found");
                return 0;
            });
            // luautils::GetMobRespawnTime(mobid): the mob's respawn time in seconds
            lua_.SetGlobal("GetMobRespawnTime", l =>
            {
                uint id = l.ToUIntArg(1);
                if (MobById(id) is MobEntity mob) return Ret(l, mob.RespawnTimeSec);
                log_($"luautils::GetMobAction: mob <{id}> was not found");
                return Ret(l, 0);
            });
        }

        private void RegisterInitBindings(Dictionary<string, LuaFunction> m)
        {
            m["setDeathType"] = l =>
            {
                if (Entity(l) is BattleEntity b) b.DeathType = (byte)l.ToIntArg(2);
                return 0;
            };
            m["getDeathType"] = l => Ret(l, Entity(l) is BattleEntity b ? b.DeathType : 0);
            m["hideHP"] = l =>
            {
                bool hide = l.ToBoolean(2);
                switch (Entity(l))
                {
                    case MobEntity mob:
                        mob.EntityFlags = hide ? mob.EntityFlags | EntityFlagHideHp : mob.EntityFlags & ~EntityFlagHideHp;
                        break;
                    case NpcEntity npc:
                        npc.EntityFlags = hide ? npc.EntityFlags | EntityFlagHideHp : npc.EntityFlags & ~EntityFlagHideHp;
                        break;
                    default:
                        log_($"Attempt to hide HP for invalid entity ({NameOf(Entity(l))})");
                        return 0;
                }
                MarkUpdate(Entity(l), EntityUpdatePacket.UpdateHp);
                return 0;
            };
            m["setAnimationSpeed"] = l =>
            {
                byte speed = (byte)l.ToIntArg(2);
                switch (Entity(l))
                {
                    case CharData c when c.AnimationSpeed != speed:
                        c.AnimationSpeed = speed;
                        if (SessionOf(c) is { } s) Push(s, CharStatus(c));
                        break;
                    case BattleEntity b when b.AnimationSpeed != speed:
                        b.AnimationSpeed = speed;
                        b.UpdateMask |= EntityUpdatePacket.UpdatePos;
                        break;
                    case NpcEntity n when n.AnimationSpeed != speed:
                        n.AnimationSpeed = speed;
                        MarkUpdate(n, EntityUpdatePacket.UpdatePos);
                        break;
                }
                return 0;
            };
            m["setNpcAlwaysRelevant"] = l =>
            {
                if (Entity(l) is NpcEntity n) n.AlwaysRelevant = l.ToBoolean(2);
                return 0;
            };
            m["getAnimation"] = l => Entity(l) switch
            {
                BattleEntity b => Ret(l, b.Animation),
                NpcEntity n => Ret(l, n.Animation),
                _ => Ret(l, 0),
            };
            m["setAggressive"] = l =>
            {
                if (Entity(l) is MobEntity mob) mob.Aggro = l.ToBoolean(2);
                else log_($"Attempting to set aggressive for invalid entity type ({NameOf(Entity(l))}).");
                return 0;
            };
            m["setRoamFlags"] = l =>
            {
                if (Entity(l) is MobEntity mob) mob.RoamFlags = (ushort)l.ToEnumArg(2); // xi::RoamFlag
                else log_($"Attempting to set roam flags for invalid entity type ({NameOf(Entity(l))}).");
                return 0;
            };
            m["setMobSkillAttack"] = l =>
            {
                if (Entity(l) is MobEntity mob) mob.SetMobMod(MobMod.AttackSkillList, (short)l.ToIntArg(2));
                else log_($"setMobSkillAttack: invalid entity type ({NameOf(Entity(l))})");
                return 0;
            };
            // CLuaBaseEntity::setSpellList: mobutils::SetSpellList (the Mob helper logs the C++ invalid-entity error)
            m["setSpellList"] = l =>
            {
                if (Mob(l) is { } mob) SetMobSpellList(mob, (ushort)l.ToIntArg(2));
                return 0;
            };
            // CLuaBaseEntity::setPet: link a master and its pet (PPet / PMaster), or clear the link on a nil petObj.
            // An NPC self is a silent no-op (the C++ returns on TYPE_NPC); a petObj that is not a battle entity does
            // nothing (the C++ dynamic_cast<CBattleEntity*> is null).
            m["setPet"] = l =>
            {
                if (Entity(l) is NpcEntity) return 0;
                var self = (BattleEntity)Entity(l);
                if (l.Type(2) != LuaNative.LUA_TUSERDATA)
                {
                    if (self.PPet != null) { self.PPet.PMaster = null; self.PPet = null; }
                    return 0;
                }
                if (l.ToObject(2) is BattleEntity pet) { self.PPet = pet; pet.PMaster = self; }
                return 0;
            };
            m["setSpawnAnimation"] = l =>
            {
                byte animation = (byte)l.ToEnumArg(2); // xi::SpawnAnimation
                switch (Entity(l))
                {
                    case MobEntity mob: mob.SpawnAnimation = animation; break;
                    case NpcEntity npc: npc.SpawnAnimation = animation; break;
                }
                return 0;
            };
            // CLuaBaseEntity::hideNPC(seconds = 15): a shown NPC disappears and comes back after the time
            m["hideNPC"] = l =>
            {
                if (Entity(l) is not NpcEntity npc)
                {
                    log_($"Attempting to hide NPC with invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                if (npc.Status != NpcEntity.StatusNormal) return 0;
                long ms = l.IsNumber(2) ? l.ToUIntArg(2) * 1000L : 15000;
                npc.Status = NpcEntity.StatusDisappear;
                DespawnNpc(npc);
                QueueNative(npc, ms, false, e =>
                {
                    var n = (NpcEntity)e;
                    n.Status = NpcEntity.StatusNormal;
                    foreach (var (c, s) in charSessions_)
                        if (SameArea(c, n)) SpawnNpcs(s, c);
                });
                return 0;
            };
            // CLuaBaseEntity::addPeriodicTrigger(triggerID, period, minuteOffset), in Vana'diel minutes
            m["addPeriodicTrigger"] = l =>
            {
                if (Entity(l) is not NpcEntity npc)
                {
                    log_($"Attempting to add periodic trigger to invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                var t = new TimeTrigger
                {
                    Npc = npc,
                    Id = (byte)l.ToIntArg(2),
                    Period = l.ToIntArg(3) * VanaTime.MIN,
                    Offset = l.ToIntArg(4) * VanaTime.MIN,
                };
                if (t.Period <= 0) return 0;
                // CTriggerHandler::insertTrigger: the current period counts as fired
                t.LastTrigger = (VanaTicksNow() - t.Offset) / t.Period;
                timeTriggers_.Add(t);
                return 0;
            };
            // CLuaBaseEntity::setMaxMP: the base max MP (not below 0), then UpdateHealth
            m["setMaxMP"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                    return 0;
                }
                b.BaseMaxMp = Math.Max(0, l.ToIntArg(2));
                CharStats.UpdateHealth(b);
                return 0;
            };
            // CLuaBaseEntity::updateToEntireZone(status, animation, matchTime): the NPC's status and animation, sent
            // to every char that has it spawned (the update goes out even past the render distance)
            m["updateToEntireZone"] = l =>
            {
                if (Entity(l) is not NpcEntity npc)
                {
                    log_($"Attempting to update NPC to entire zone with invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                npc.Status = (byte)l.ToEnumArg(2); // xi::Status
                npc.Animation = (byte)l.ToIntArg(3);
                if (l.Type(4) == LuaNative.LUA_TBOOLEAN && l.ToBoolean(4))
                    LocalVars(npc)["TransportTimestamp"] = (uint)(UnixNow() - VanaTime.EpochUnixSec);
                BroadcastNpcUpdate(npc, EntityUpdatePacket.UpdateCombat);
                return 0;
            };
            m["setCrystalElement"] = l =>
            {
                if (Entity(l) is MobEntity mob) mob.Element = (byte)l.ToEnumArg(2); // ELEMENT
                else log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                return 0;
            };
        }
    }
}
