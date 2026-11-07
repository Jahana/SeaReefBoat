using System;
using System.Collections.Generic;
using XI.Map;
using XI.Script;

namespace XI.Zone
{
    // Charm. A BST's: battleutils::applyCharm for a mob (the char's PPet, the pet controller for the charm time, the
    // claim and enmity let go), petutils::DetachPet's charmed mob branch (turning on its master when in range, or let
    // go by Leave or the master's death; the other mobs forget it), ExtendCharm. A mob's: applyCharm for a char (its pet
    // gone, its claim let go, inactive 5 s) and CPlayerCharmController (following its charmer, engaging and swinging at
    // the charmer's target, out of it when the charmer is gone; battleutils::unCharm through the CHARM effect's loss).
    // The charm / uncharm / isCharmed / extendCharm bindings. A charmed mob stays a MobEntity in the zone's mob list and
    // runs CPetController through the pet functions of ZoneHost.PlayerPets.cs; it keeps an enmity list of its own (the
    // mobs hitting it) and its spells give its foes enmity like a player's. A charmed char keeps its fight apart
    // from the player attack state (CharmTarget, the swings through MeleeSwing); its trusts are sent away.
    public sealed partial class ZoneHost
    {
        private const byte AllegianceMob = 0;
        private const ushort AbilityLeave = 71;
        private const float CharmRoamDistance = 2.1f; // CPlayerCharmController::RoamDistance
        private const long CharmEngageDelayMs = 3000;

        // a mob charmed by a char (CPetController over a CMobEntity)
        private static bool IsCharmedPet(MobEntity m) => m is not PetEntity && m.IsCharmed && m.PMaster is CharData;

        // CAIContainer::Tick for a charmed mob: CPetController::Tick, then the state on top (a skill, a spell, else the
        // attack state's swing)
        private void TickCharmedMob(MobEntity m, long now)
        {
            PetControllerTick(m, now);
            if (!IsCharmedPet(m)) return;
            if (m.SkillState != null) TickMobSkill(m, now);
            else if (m.MagicState != null) TickMagic(m, now);
            else if (TickMobAbility(m, now)) { }
            else if (m.Engaged && !m.IsDead) UpdatePetAttackState(m, now);
        }

        // battleutils::applyCharm
        private void ApplyCharm(BattleEntity charmer, BattleEntity victim, long durationMs)
        {
            long now = NowMs();
            if (victim is MobEntity mob and not PetEntity)
            {
                mob.IsCharmed = true;
                mob.PMaster = charmer;
                charmer.PPet = mob;
                // make the mob disengage, and clear its enmity list
                if (mob.Engaged) MobDisengage(mob, now);
                mob.Enmity.Clear();
                // the pet controller: no weapon skills of its own (CPetController's SetWeaponSkillEnabled(false))
                mob.MobAbilityEnabled = false;
                mob.CharmTimeMs = now + durationMs;
                // this will make him transition back to roaming if sleeping
                mob.Animation = Anim.None;
                if (charmer is CharData c)
                {
                    c.UpdateMask |= CharStats.UpdateHp;
                    loader_.BuildAbilityTable(c);
                    Array.Clear(c.PetCommands);
                    PushTo(c, LoginPackets.CommandData(c));
                    PushTo(c, CharStatus(c));
                    PushTo(c, PetSyncPacket.Create(c));
                }
                foreach (var member in PartyOf(charmer))
                    if (member is CharData pc && pc.ClaimedMob == mob) pc.ClaimedMob = null;
                mob.OwnerId = 0;
                mob.UpdateMask |= EntityUpdatePacket.UpdateStatus;
            }
            else if (victim is CharData pc)
            {
                if (pc.PPet != null) DespawnPet(pc);
                ClearTrusts(pc);
                RelinquishClaim(pc, now);
                pc.PMaster = charmer;
                pc.IsCharmed = true;
                // CPlayerCharmController: its own path; the player's attack state gives way to the charmer's fight
                pc.CharmPath = new PathFind(pc, this);
                if (pc.AttackTarget != null) EndAttackState(pc, now);
                pc.CharmTarget = null;
                pc.UpdateMask |= LoginPackets.UpdateAllChar;
                // Prevent auto attacks for a little bit to simulate retail
                EnterInactive(pc, 5000, false, false, now);
            }
            else return;
            byte allegiance = charmer is CharData cc ? cc.Allegiance : ((MobEntity)charmer).Allegiance;
            if (victim is MobEntity v)
            {
                v.Allegiance = allegiance;
                v.UpdateMask |= EntityUpdatePacket.UpdateHp;
            }
            else if (victim is CharData vc)
            {
                vc.Allegiance = allegiance;
                vc.UpdateMask |= CharStats.UpdateHp;
            }
        }

        // petutils::DetachPet for a charmed mob: alive, it disengages and turns on its master when in range (else the
        // claim goes), its EXP dirty unless at full HP; let go by Leave or the master's death, it forgets everything.
        // Then it is a mob again, the other mobs forget it, and the master's abilities, status and pet sync follow.
        private void DetachCharmedMob(CharData master, MobEntity mob)
        {
            long now = NowMs();
            if (mob.IsAlive && !mob.IsDead)
            {
                if (mob.Engaged) MobDisengage(mob, now);
                // charm time is up, mob attacks player now
                if (mob.Enmity.InRange(master))
                {
                    mob.Enmity.UpdateEnmity(master, 0, 0);
                    mob.BattleTarget = master;
                }
                else
                {
                    mob.OwnerId = 0;
                    mob.UpdateMask |= EntityUpdatePacket.UpdateStatus;
                }
                // dirty exp if not full
                mob.GiveExp = mob.Hpp == 100;
                // master using leave command
                if (master.AbilityState?.Ability.Id == AbilityLeave || master.Hp <= 0)
                {
                    mob.Enmity.Clear();
                    mob.BattleTarget = null;
                    mob.OwnerId = 0;
                    mob.UpdateMask |= EntityUpdatePacket.UpdateStatus;
                }
            }
            else
            {
                mob.OwnerId = 0;
                mob.UpdateMask |= EntityUpdatePacket.UpdateStatus;
            }

            mob.IsCharmed = false;
            mob.Allegiance = AllegianceMob;
            mob.CharmTimeMs = 0;
            mob.PMaster = null;
            mob.SetMobMod(MobMod.Bodyguard, 0);
            // the mob controller again
            mob.MobAbilityEnabled = true;

            // clear all enmity towards a charmed mob when it is released
            foreach (var other in mob.Notoriety) other.Enmity.Clear(mob.Id);

            loader_.BuildAbilityTable(master);
            master.PPet = null;
            PushTo(master, CharStatus(master));
            PushTo(master, LoginPackets.CommandData(master));
            PushTo(master, PetSyncPacket.Create(master));
        }

        // battleutils::unCharm for a char, with ~CPlayerCharmController: the player controller again, no master, out of
        // the fight, its path gone, a player's allegiance
        private void UnCharm(CharData c)
        {
            c.IsCharmed = false;
            c.PMaster = null;
            CharmDisengage(c);
            c.CharmPath = null;
            c.Allegiance = CharData.AllegiancePlayer;
            c.UpdateMask |= LoginPackets.UpdateAllChar;
        }

        // CPlayerCharmController::Tick, then its attack state: with the charmer gone or dead the CHARM effect goes (and
        // with it the charm); engaged, the combat tick, else the roam
        private void TickCharmedChar(MapSession s, CharData c, long now)
        {
            if (c.Hp <= 0) return;
            if (c.PMaster is not MobEntity master || !master.IsSpawned || !master.IsAlive || master.IsDead)
            {
                c.StatusEffects.DelStatusEffect(EffectId.CharmI);
                return;
            }
            if (c.InactiveState != null) return;
            if (c.CharmTarget != null) CharmCombatTick(c, master, now);
            else CharmRoamTick(c, master, now);
            CharmAttackState(c, now);
            if (c.UpdateMask != 0) SendUpdateMask(s, c);
        }

        // CPlayerCharmController::DoCombatTick: out of the fight with its charmer, on the charmer's target, closing in
        // until it can swing
        private void CharmCombatTick(CharData c, MobEntity master, long now)
        {
            if (!master.Engaged)
            {
                CharmDisengage(c);
                return;
            }
            if (master.BattleTarget != c.CharmTarget) c.CharmTarget = master.BattleTarget == c ? null : master.BattleTarget;
            if (c.CharmTarget is not { } target) return;
            c.CharmPath.LookAt(target.X, target.Y, target.Z);
            if (!CharmInReach(c, target) && c.Speed > 0)
            {
                c.CharmPath.PathAround(target.X, target.Y, target.Z, 2.0f, PathFlag.Run);
                c.CharmPath.FollowPath(now);
            }
        }

        // CPlayerCharmController::DoRoamTick: into the charmer's fight, and after the charmer once more than 2.1 yalms
        // away (a warp to it beyond 35 yalms or with no path)
        private void CharmRoamTick(CharData c, MobEntity master, long now)
        {
            if (master.Engaged && master.BattleTarget is { } target && target != c)
            {
                // CAIContainer::Internal_Engage: the attack state, its first swing 3 s off
                c.CharmTarget = target;
                c.BattleStartMs = now;
                c.SwingDelayMs = MeleeFor().WeaponDelay(c, false);
                c.SwingAtMs = now + CharmEngageDelayMs;
                c.Animation = Anim.Attack;
                c.UpdateMask |= CharStats.UpdateHp;
            }
            float currentDistance = Distance(c, master);
            if (currentDistance <= CharmRoamDistance) return;
            if (currentDistance < 35.0f && c.CharmPath.PathAround(master.X, master.Y, master.Z, 2.0f, PathFlag.Run))
            {
                c.CharmPath.FollowPath(now);
            }
            else if (c.Speed > 0)
            {
                // CPathFind::WarpTo
                c.CharmPath.Clear();
                (c.X, c.Y, c.Z) = NearPosition(master, CharmRoamDistance, MathF.PI);
                c.Moving = 0;
                c.UpdateMask |= CharStats.UpdatePos;
            }
        }

        // the charmed char's CAttackState: a round whenever one is due and the target is in reach, until it is down
        private void CharmAttackState(CharData c, long now)
        {
            if (c.CharmTarget is not { } target) return;
            bool down = target is MobEntity m ? !m.IsAlive || m.IsDead || !m.IsSpawned : target.Hp <= 0;
            if (down || !SameArea(target, c))
            {
                CharmDisengage(c);
                return;
            }
            if (now < c.SwingAtMs) return;
            if (CharmInReach(c, target))
            {
                c.SwingAtMs += c.SwingDelayMs;
                if (Attack(c, target, now) is { } attack) PushCharInRange(c, Battle2Packets.Battle2(attack), true);
            }
            if (c.SwingAtMs < now) c.SwingAtMs = now;
        }

        // CCharEntity::CanAttack's reach for the charmed char
        private static bool CharmInReach(CharData c, BattleEntity target) =>
            Distance(c, target) <= c.ModelHitboxSize10 / 10.0f + 2.0f + HitboxSize(target);

        private static void CharmDisengage(CharData c)
        {
            c.CharmTarget = null;
            if (c.Animation == Anim.Attack)
            {
                c.Animation = Anim.None;
                c.UpdateMask |= CharStats.UpdateHp;
            }
        }

        // petutils::ExtendCharm: a charmed mob's charm time, longer by minSeconds .. maxSeconds at random
        private void ExtendCharm(BattleEntity pet, int minSeconds, int maxSeconds)
        {
            if (!(pet is MobEntity and not PetEntity && pet.IsCharmed)) return;
            if (minSeconds > maxSeconds || maxSeconds == 0) return;
            pet.CharmTimeMs += MobRng.Next(minSeconds, maxSeconds) * 1000L;
        }

        private void RegisterCharmBindings(Dictionary<string, LuaFunction> m)
        {
            // charm(target[, seconds]): no seconds is a charm the status effect ends (a charmed char)
            m["charm"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                long seconds = l.IsNumber(3) ? (long)(uint)l.ToIntegral(3) : 0;
                if (l.ToObject(2) is BattleEntity target) ApplyCharm(b, target, seconds * 1000);
                return 0;
            };
            // battleutils::unCharm: only a char comes out of it this way (a charmed mob leaves through DetachPet)
            m["uncharm"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                if (b is CharData c) UnCharm(c);
                return 0;
            };
            m["isCharmed"] = l =>
            {
                if (Entity(l) is BattleEntity b) return Ret(l, b.IsCharmed);
                log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                return Ret(l, false);
            };
            m["extendCharm"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                    return 0;
                }
                ExtendCharm(b, l.ToIntArg(2), l.ToIntArg(3));
                return 0;
            };
        }
    }
}
