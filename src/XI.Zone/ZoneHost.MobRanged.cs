using System;
using XI.Map;

namespace XI.Zone
{
    // A mob's ranged attack: the controller's shot (CMobController::DoCombatTick after the TP move: ranged attacks
    // enabled, the target within GetRangedAttackRange, the free phase over and the state free to change; FaceTarget,
    // CAIContainer::Internal_RangedAttack refusing an untargetable target, TapDeaggroTime) and CRangeState for a mob:
    // init (the target, facing it, the animation, the range, the aim time from GetRangedWeaponDelay and
    // GetRangedDelayReduction, the 2850 ms return delay, a free phase of 1.1 s, or 6.5 to 8 s when it shot from melee
    // range, RANGE_START and the RangedStart BATTLE2), Update (the shot through CBattleEntity::OnRangedAttack,
    // RANGE_STATE_EXIT, the error messages kept from chars) and the end (m_LastRangedAttackTime = entry + aim + free
    // phase). The attack state's swing countdown is held while it runs; a stun or other forced inactive state drops it
    // (CRangeState::CanInterrupt). SetupRangedAttack turns it on for fomor rangers and ninjas; setRangedAttackEnabled
    // and isRangedAttackEnabled are the bindings. CanUseRangedAttack checks the line of sight (CanSeeTarget,
    // ZoneHost.XiMesh.cs); a pet's combat tick (ZoneHost.PlayerPets.cs) and a trust's AI (ZoneHost.Trusts.cs) run the
    // same shot.
    public sealed partial class ZoneHost
    {
        private const long RangedReturnWeaponDelayMobMs = 2850; // CRangeState::init: a mob puts its weapon away slower
        private const long RangedFreePhaseMobMs = 1100;         // CRangeState::m_freePhaseTimeMob

        // CMobEntity::GetRangedAttackRange: 14, as on all retail fomor, unless MobMod.RangedAttackRange says otherwise
        private static float MobRangedAttackRange(MobEntity m)
        {
            int range = m.GetMobMod(MobMod.RangedAttackRange);
            return range > 0 ? range : 14.0f;
        }

        // the ranged half of DoCombatTick: true once a shot was started
        private bool MobTryRangedAttack(MobEntity m, long now)
        {
            var target = m.BattleTarget;
            if (!m.RangedAttackEnabled || target == null || Distance(m, target) > MobRangedAttackRange(m) ||
                now < m.LastRangedAttackMs || !CanChangeState(m))
                return false;
            FaceTarget(m, target);
            // CAIContainer::Internal_RangedAttack
            if (IsUntargetable(target) || !StartMobRanged(m, target, now)) return false;
            m.DeaggroTimeMs = now; // TapDeaggroTime
            m.LastRangedAttackMs = now;
            return true;
        }

        // CRangeState::init for a mob: a refusal's error message reaches no one
        private bool StartMobRanged(MobEntity m, BattleEntity target, long now)
        {
            if (!ValidSkillTarget(target, m, TargetEnemy) || !MobRangedCanUse(m, target, false, out _)) return false;
            if (Distance(m, target) > MobRangedAttackRange(m)) return false;

            short delay = RangedDelayReduction(m, RangedWeaponDelay(m));
            var st = new RangedState
            {
                TargId = target.TargId, Target = target, EntryMs = now, AimMs = delay, StartX = m.X, StartZ = m.Z,
                ReturnWeaponDelayMs = RangedReturnWeaponDelayMobMs,
                SwingRemainingMs = Math.Max(0, m.SwingAtMs - now),
            };
            // Seems to have a random factor on to when it can shoot next. 1 or 2 melee auto attacks
            if (target is CharData tc && Distance(m, target) <= MeleeRange(m, tc)) st.FreePhaseMs = 6500 + MobRng.Next(0, 1500);
            m.RangedState = st;

            var action = new ActionPacket { ActorId = m.Id, ActionType = RangedCategoryStart, ActionId = (uint)FourCC.RangedStart };
            action.AddTarget(m.Id).AddResult(); // an empty result
            TriggerListener(m, "RANGE_START", m, action);
            PushMobInRange(m, Battle2Packets.Battle2(action));
            return true;
        }

        // CRangeState::CanUseRangedAttack for a mob: no weapon or free-phase checks; cannotSee is the facing refusal
        // the end of the aim lets through; the line of sight is checked at the start only
        private bool MobRangedCanUse(MobEntity m, BattleEntity target, bool isEndOfAttack, out bool cannotSee)
        {
            cannotSee = false;
            if (target == null) return false;
            if (!Facing(m, target))
            {
                cannotSee = true;
                return false;
            }
            if (!isEndOfAttack && !CanSeeTarget(m, target)) return false;
            return m.Animation is Anim.None or Anim.Attack;
        }

        // the state on top of a mob's AI tick
        private void TickMobRanged(MobEntity m, long now)
        {
            var st = m.RangedState;
            if (UpdateMobRanged(m, st, now)) EndMobRanged(m, now);
        }

        // CRangeState::Update for a mob: true once the state is done (HasMoved is a char's alone)
        private bool UpdateMobRanged(MobEntity m, RangedState st, long now)
        {
            if (m.IsAlive && now > st.EntryMs + st.AimMs && !st.Completed)
            {
                var target = st.Target != null && EntityExists(st.Target) && ValidSkillTarget(st.Target, m, TargetEnemy) ? st.Target : null;
                bool canUse = MobRangedCanUse(m, target, true, out bool cannotSee);

                var action = new ActionPacket();
                // Target is untargetable (e.g., Super Jump, Worm roaming, Antlion burrowing etc.)
                if (target != null && IsUntargetable(target))
                {
                    RangedInterruptAttack(m, st, action);
                }
                else if (!canUse && !cannotSee)
                {
                    RangedInterruptAttack(m, st, action);
                }
                else
                {
                    if (target == null || Distance(m, target) > MobRangedAttackRange(m)) st.OutOfRange = true;
                    OnRangedAttack(m, st, target, action);
                    // Only send packet if action was populated (e.g. interrupts return early)
                    if (action.Targets.Count > 0) PushMobInRange(m, Battle2Packets.Battle2(action));
                    if (target is CharData tc)
                    {
                        EnmityHandleKillshot(m, tc);
                        if (SessionOf(tc) is { } s) SendUpdateMask(s, tc);
                    }
                    TriggerListener(m, "RANGE_STATE_EXIT", m, target, action);
                }
                st.Completed = true;
            }

            if (st.Completed && now > st.EntryMs + st.AimMs + st.ReturnWeaponDelayMs)
            {
                m.LastRangedAttackMs = st.EntryMs + st.AimMs + st.FreePhaseMs;
                return true;
            }
            return false;
        }

        // the state popped (done, or interrupted): the attack state's swing picks up where it stood
        private static void EndMobRanged(MobEntity m, long now)
        {
            if (m.RangedState is not { } st) return;
            m.RangedState = null;
            m.SwingAtMs = now + st.SwingRemainingMs;
        }
    }
}
