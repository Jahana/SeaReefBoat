using System;
using System.Collections.Generic;
using XI.Map;
using XI.Script;

namespace XI.Zone
{
    // GP_CLI_COMMAND_ACTION Shoot (0x01A, ActionID 0x10): a char's ranged attack. Ports CPlayerController::RangedAttack,
    // CRangeState (init, Update, CanUseRangedAttack, InterruptRangedAttack, HasMoved, the end of the attack) and
    // CBattleEntity::OnRangedAttack for a char. init checks the target, the ranged weapon and its ammo (Barrage drops
    // when throwing), facing the target, the free phase (main.RANGED_ATTACK_FREE_PHASE_DELAY) since the last shot, the
    // animation and the 25' range, works out the aim time (GetRangedWeaponDelay, GetRangedDelayReduction: Snapshot and
    // Velocity Shot, then Rapid Shot), fires RANGE_START and pushes the RangedStart BATTLE2. After the aim time the shot
    // is checked again (moving more than 0.3' interrupts with "You move and interrupt your aim"); OnRangedAttack rolls
    // Paralyze, the hits (Barrage, Sange, Triple Shot, Double Shot), each hit's rate, critical and pDIF in Lua, the skill
    // ups, Recycle (Unlimited Shot, the Recycle merits and job points), the distance message, the ranged damage
    // multipliers (attackutils::CheckForDamageMultiplier) and TakePhysicalDamage's ranged path, then the claim, the ammo
    // used (RemoveAmmo, unequipping the last one), Camouflage and processActionEffectFlags. The state ends 800 ms
    // (m_returnWeaponDelay) after the aim time; CanChangeState is false while it runs, and the ranged slot (or the ammo
    // slot without a ranged weapon) cannot be changed.
    // OnRangedAttack, the interrupts, TakePhysicalDamage's ranged path and the ranged battleutils take a mob as well
    // (its CRangeState and the controller's shot are ZoneHost.MobRanged.cs).
    // A char's shot that hit runs the ammo's additional effect, else the weapon's (xi.additionalEffect.attack or the
    // item script's onItemAdditionalEffect); each ammo used counts for Scavenge (TrackArrowUsageForScavenge).
    // A char the char may harm is a target (PvP 2); CanSeeTarget (the line of sight) gates the shot.
    // Not ported: pets and trusts as targets; interrupting the state from outside (zoning, InterruptStates).
    public sealed partial class ZoneHost
    {
        // msg_basic.h
        private const ushort MsgRangedShadowAbsorb = 31, MsgRangedCannotPerformAction = 71, MsgRangedUsesSange = 77,
            MsgRangedIsParalyzed2 = 84, MsgRangedWaitLonger = 94, MsgRangedUsesBarrage = 157, MsgRangedNoRangedWeapon = 216,
            MsgRangedCannotSee = 217, MsgRangedMoveAndInterrupt = 218, MsgRangedHit = 352, MsgRangedCrit = 353,
            MsgRangedMiss = 354, MsgRangedAbsorbs = 382, MsgRangedCannotAttackTarget = 446, MsgRangedSquarely = 576,
            MsgRangedPummels = 577;

        // ActionCategory::RangedFinish / RangedStart
        private const byte RangedCategoryFinish = 2, RangedCategoryStart = 12;

        // xi::StatusEffect ids EffectId lacks
        private const ushort RangedEffectDeodorize = 70, RangedEffectBarrage = 73, RangedEffectUnlimitedShot = 115,
            RangedEffectStealthShot = 350, RangedEffectFlashyShot = 351, RangedEffectSange = 352, RangedEffectVelocityShot = 371,
            RangedEffectYonin = 420, RangedEffectInnin = 421, RangedEffectDoubleShot = 433, RangedEffectTripleShot = 467;

        // xi::Merit, the traits and JP_AMMO_CONSUMPTION
        private const ushort RangedMeritCritHitRate = 324, RangedMeritEnemyCritRate = 326, RangedMeritRapidShotRate = 1032,
            RangedMeritSnapshot = 2692, RangedMeritRecycle = 2694;
        private const int RangedTraitSnapshot = 83, RangedTraitRecycle = 84, RangedJpAmmoConsumption = 0x225;

        private const float RangedAttackRange = 25.0f;      // CBattleEntity::GetRangedAttackRange
        private const long RangedReturnWeaponDelayMs = 800; // CRangeState::m_returnWeaponDelay

        // CRangeState (a mob's keeps its target, its 2850 ms return delay, its free phase and the attack state's
        // countdown held while it runs)
        internal sealed class RangedState
        {
            public ushort TargId;
            public BattleEntity Target;
            public long EntryMs, AimMs;
            public long ReturnWeaponDelayMs = RangedReturnWeaponDelayMs, FreePhaseMs = RangedFreePhaseMobMs, SwingRemainingMs;
            public float StartX, StartZ;
            public bool Completed, RapidShot, OutOfRange;
        }

        // CPlayerController::RangedAttack
        private void RangedAttack(MapSession s, CharData c, ushort targid)
        {
            // canAct and CanChangeState
            if (!CanAct(c) || !CanChangeState(c) || c.ItemUse != null)
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgRangedWaitLonger));
                return;
            }
            // the target resolved: an untargetable one is refused silently (here and in CAIContainer::Internal_RangedAttack)
            if (AbilityEntityByTargId(c, targid) is { } rt && IsUntargetable(rt)) return;
            StartRanged(s, c, targid);
        }

        // CRangeState::init: a refusal pushes its error message to the char
        private void StartRanged(MapSession s, CharData c, ushort targid)
        {
            var target = WsValidTarget(c, targid, false, out var errMsg);
            if (target == null || errMsg != null)
            {
                if (errMsg != null) Push(s, errMsg);
                return;
            }

            if (!RangedCanUse(c, target, false, out errMsg))
            {
                Push(s, errMsg);
                return;
            }

            if (Distance(c, target) > RangedAttackRange)
            {
                Push(s, SelfPackets.BattleMessage(c.Id, c.TargId, target.Id, target.TargId, 0, 0, MsgTooFarAway));
                return;
            }

            // GetRangedDelayReduction is 2 of the 3 steps of `Ranged Weapon Delay x (1 - Snapshot) x (1 - Velocity Shot)
            // x (1 - Rapid Shot)`; if Rapid Shot fires it does the third
            short delay = RangedWeaponDelay(c);
            delay = RangedDelayReduction(c, delay);

            // Rapid Shot: not for throwing weapons (chakrams and shurikens)
            bool rapidShot = false;
            bool isThrowing = c.RangedWeapon is { Skill: Skill.Throwing } || c.AmmoWeapon is { Skill: Skill.Throwing };
            if (!isThrowing)
            {
                int chance = c.GetMod(Mod.RAPID_SHOT) + (c.Merits?.Value(c, RangedMeritRapidShotRate) ?? 0);
                if (chance > 0 && MobRng.Next(100) < chance)
                {
                    // reduce delay by 2-50%
                    delay = (short)(delay * (1.0f - MobRng.Next(2, 50) / 100.0f));
                    rapidShot = true;
                }
            }

            var st = new RangedState
            {
                TargId = targid, EntryMs = NowMs(), AimMs = delay, StartX = c.X, StartZ = c.Z, RapidShot = rapidShot,
            };
            c.RangedState = st;

            var action = new ActionPacket { ActorId = c.Id, ActionType = RangedCategoryStart, ActionId = (uint)FourCC.RangedStart };
            action.AddTarget(c.Id).AddResult(); // an empty result
            TriggerListener(c, "RANGE_START", c, action);
            PushCharInRange(c, Battle2Packets.Battle2(action), true);
        }

        // CRangeState::CanUseRangedAttack for a char
        private bool RangedCanUse(CharData c, BattleEntity target, bool isEndOfAttack, out BasicPacket errMsg)
        {
            errMsg = null;
            if (target == null)
            {
                errMsg = SelfPackets.BattleMessage(c, 0, 0, MsgRangedCannotAttackTarget);
                return false;
            }

            var ranged = c.RangedWeapon;
            var ammo = c.AmmoWeapon;
            if (!(ranged != null || ammo is { Skill: Skill.Throwing }))
            {
                errMsg = SelfPackets.BattleMessage(c, 0, 0, MsgRangedNoRangedWeapon);
                return false;
            }

            int skillType = ranged != null ? ranged.Skill : ammo.Skill;
            switch (skillType)
            {
                case Skill.Throwing:
                    // remove barrage, doesn't work here
                    c.StatusEffects.DelStatusEffect(RangedEffectBarrage);
                    break;
                case Skill.Archery:
                case Skill.Marksmanship:
                    if (c.AmmoWeapon != null) break;
                    errMsg = SelfPackets.BattleMessage(c, 0, 0, MsgRangedNoRangedWeapon);
                    return false;
                default:
                    errMsg = SelfPackets.BattleMessage(c, 0, 0, MsgRangedNoRangedWeapon);
                    return false;
            }

            if (!Facing(c, target))
            {
                errMsg = SelfPackets.BattleMessage(c.Id, c.TargId, target.Id, target.TargId, 0, 0, MsgRangedCannotSee);
                return false;
            }

            if (!isEndOfAttack && !CanSeeTarget(c, target))
            {
                errMsg = SelfPackets.BattleMessage(c.Id, c.TargId, target.Id, target.TargId, 0, 0, MsgRangedCannotPerformAction);
                return false;
            }

            // make sure player is waiting the appropriate time between ranged attacks
            if (NowMs() - c.LastRangedAttackMs < ChatCfg.RangedAttackFreePhaseDelay)
            {
                errMsg = SelfPackets.BattleMessage(c.Id, c.TargId, target.Id, target.TargId, 0, 0, MsgRangedWaitLonger);
                return false;
            }

            if (c.Animation != Anim.None && c.Animation != Anim.Attack)
            {
                errMsg = SelfPackets.BattleMessage(c.Id, c.TargId, target.Id, target.TargId, 0, 0, MsgRangedCannotPerformAction);
                return false;
            }
            return true;
        }

        private void TickRanged()
        {
            long now = NowMs();
            foreach (var (c, s) in new List<KeyValuePair<CharData, MapSession>>(charSessions_))
            {
                if (c.RangedState == null) continue;
                // CCharEntity::Die clears the state stack
                if (!c.IsAlive) EndRanged(c, c.RangedState, now);
                else if (UpdateRanged(s, c, c.RangedState, now)) EndRanged(c, c.RangedState, now);
            }
        }

        // CRangeState::Update: true once the state is done
        private bool UpdateRanged(MapSession s, CharData c, RangedState st, long now)
        {
            if (c.IsAlive && now > st.EntryMs + st.AimMs && !st.Completed)
            {
                var target = WsValidTarget(c, st.TargId, false, out var errMsg);
                if (!RangedCanUse(c, target, true, out var canUseErr)) errMsg = canUseErr;

                if (RangedHasMoved(c, st)) errMsg = SelfPackets.BattleMessage(c, 0, 0, MsgRangedMoveAndInterrupt);

                var action = new ActionPacket();
                // Target is untargetable (e.g., Super Jump, Worm roaming, Antlion burrowing etc.)
                if (IsUntargetable(target))
                {
                    RangedInterruptAttack(c, st, action);
                }
                else if (errMsg != null && BitConverter.ToUInt16(errMsg.Buffer, 24) != MsgRangedCannotSee)
                {
                    Push(s, errMsg);
                    RangedInterruptAttack(c, st, action);
                }
                else
                {
                    if (target == null || Distance(c, target) > RangedAttackRange) st.OutOfRange = true;
                    OnRangedAttack(c, st, target, action);
                    // Only send packet if action was populated (e.g. interrupts return early)
                    if (action.Targets.Count > 0) PushCharInRange(c, Battle2Packets.Battle2(action), true);
                    TriggerListener(c, "RANGE_STATE_EXIT", c, target, action);
                }
                st.Completed = true;
            }

            if (st.Completed && now > st.EntryMs + st.AimMs + RangedReturnWeaponDelayMs)
            {
                c.LastRangedAttackMs = st.EntryMs + st.AimMs + RangedReturnWeaponDelayMs;
                return true;
            }
            return false;
        }

        // CRangeState::Cleanup (empty); the suspended attack state resumes where its swing timer stood
        private void EndRanged(CharData c, RangedState st, long now)
        {
            c.RangedState = null;
            if (c.AttackTarget != null) c.SwingAtMs += now - st.EntryMs;
        }

        // CRangeState::HasMoved: more than 0.3' from where the aim started, ignoring height
        private static bool RangedHasMoved(CharData c, RangedState st)
        {
            float dx = c.X - st.StartX, dz = c.Z - st.StartZ;
            return MathF.Sqrt(dx * dx + dz * dz) > 0.3f;
        }

        // CRangeState::InterruptRangedAttack: interrupted players only wait the 800 ms until the next shot
        private void RangedInterruptAttack(BattleEntity b, RangedState st, ActionPacket action)
        {
            st.AimMs = 0;
            RangedInterrupt(b);
            TriggerListener(b, "RANGE_STATE_EXIT", b, null, action);
        }

        // ActionInterrupts::RangedInterrupt
        private void RangedInterrupt(BattleEntity b)
        {
            var action = new ActionPacket { ActorId = b.Id, ActionType = RangedCategoryStart, ActionId = (uint)FourCC.RangedInterrupt };
            action.AddTarget(b.Id).AddResult().Animation = AnimWsInterrupt;
            PushCasterInRange(b, Battle2Packets.Battle2(action));
        }

        // ActionInterrupts::RangedParalyzed
        private void RangedParalyzed(BattleEntity b)
        {
            var action = new ActionPacket { ActorId = b.Id, ActionType = ActionCategory.MagicFinish };
            var r = action.AddTarget(b.Id).AddResult();
            r.Animation = AnimWsInterrupt;
            r.MessageId = MsgRangedIsParalyzed2;
            RangedInterrupt(b);
            PushCasterInRange(b, Battle2Packets.Battle2(action));
        }

        // CBattleEntity::OnRangedAttack: only a char has ammo, Sange, Triple / Double Shot, skill-ups, Recycle, the
        // distance message, the damage multipliers, the claim and Camouflage; a mob shoots once, or Barrage's shots
        private void OnRangedAttack(BattleEntity b, RangedState st, BattleEntity target, ActionPacket action)
        {
            if (target == null) return;
            var c = b as CharData;
            bool isChar = c != null;
            var rng = MobRng;
            long now = NowMs();

            // battleutils::IsParalyzed
            if (rng.Next(100) < b.GetMod(Mod.PARALYZE))
            {
                RangedParalyzed(b);
                return;
            }

            int damage, totalDamage = 0;

            action.ActorId = b.Id;
            action.ActionType = RangedCategoryFinish;
            action.ActionId = (uint)FourCC.RangedFinish;
            var r = action.AddTarget(target.Id).AddResult();
            r.MessageId = MsgRangedHit;

            WeaponInfo item = null, ammo = null;
            bool ammoThrowing = false, rangedThrowing = false;
            int slot = CharStats.SlotRanged;
            int ammoQuantity = 0;
            // Only characters have ammo
            if (isChar)
            {
                item = c.RangedWeapon;
                ammo = c.AmmoWeapon;
                ammoThrowing = ammo is { Skill: Skill.Throwing };
                rangedThrowing = item is { Skill: Skill.Throwing };
                if (ammoThrowing)
                {
                    slot = CharStats.SlotAmmo;
                    item = null;
                }
                if (rangedThrowing) ammo = null;
                ammoQuantity = ammo == null ? 0 : (int)(CharStats.EquippedItem(c, CharStats.SlotAmmo)?.Quantity ?? 0);
            }

            byte shadowsTaken = 0;
            byte hitCount = 1;     // 1 hit by default
            byte realHits = 0;     // the real number of hits, the TP multiplier
            int ammoConsumed = 0;
            bool hitOccured = false, wasCritical = false;
            bool isBarrage = b.StatusEffects.HasStatusEffectWithSubId(RangedEffectBarrage, 0u);
            bool isSange = isChar && c.StatusEffects.HasStatusEffect(RangedEffectSange) && c.GetMod(Mod.SANGE_MULTI_HIT) > 0;

            // Player Barrage check
            if (isChar && !ammoThrowing && !rangedThrowing && isBarrage)
            {
                hitCount += RangedBarrageShotCount(b);
            }
            else if (!isChar && isBarrage) // Mobs and trusts have barrage
            {
                hitCount += RangedBarrageShotCount(b);
            }
            else if (isChar && ammoThrowing && isSange)
            {
                int shadows = Math.Clamp((int)c.GetMod(Mod.UTSUSEMI), 0, 7);
                c.StatusEffects.DelStatusEffect(EffectId.CopyImage);
                hitCount += (byte)shadows;
                if (ammo != null && ammoQuantity < hitCount) hitCount = (byte)ammoQuantity;
            }
            else if (isChar && c.StatusEffects.HasStatusEffect(RangedEffectTripleShot) && rng.Next(100) < c.GetMod(Mod.TRIPLE_SHOT_RATE))
            {
                hitCount = 3;
            }
            else if (isChar && c.StatusEffects.HasStatusEffect(RangedEffectDoubleShot) && rng.Next(100) < c.GetMod(Mod.DOUBLE_SHOT_RATE))
            {
                hitCount = 2;
            }

            // Loop for barrage hits. Once there is a miss the loop ends
            for (byte i = 1; i <= hitCount; ++i)
            {
                if (rng.Next(100) < RangedHitRate(b, target) && !st.OutOfRange)
                {
                    // Absorbed by shadow
                    if (IsAbsorbByShadow(target, b))
                    {
                        shadowsTaken++;
                    }
                    else
                    {
                        bool isCritical = rng.Next(100) < RangedCritHitRate(b, target);
                        float pdif = RangedDamageRatio(b, target, isCritical);
                        if (isCritical)
                        {
                            wasCritical = true;
                            r.MessageId = MsgRangedCrit;
                        }

                        // We hit the target
                        hitOccured = true;
                        realHits++;
                        damage = (int)((RangedWeaponDmg(b) + WsCallGlobalInt("xi.combat.physical.calculateRangedStatFactor", b, target)) * pdif);

                        // Check for char skill ups
                        if (isChar)
                        {
                            if (slot == CharStats.SlotRanged && item != null) TrySkillUp(c, item.Skill, target.MLvl, false, false);
                            else if (slot == CharStats.SlotAmmo && ammo != null) TrySkillUp(c, ammo.Skill, target.MLvl, false, false);
                        }
                        totalDamage += damage;
                    }
                }
                else // Miss
                {
                    r.Resolution = 1;
                    r.MessageId = MsgRangedMiss;
                    hitCount = i;
                }

                if (!isChar) continue;

                ushort recycleChance = (ushort)c.GetMod(Mod.RECYCLE);
                if (CharStats.HasTrait(c, RangedTraitRecycle)) recycleChance += (ushort)(c.Merits?.Value(c, RangedMeritRecycle) ?? 0);
                recycleChance += (ushort)JobPointUtil.Value(c, RangedJpAmmoConsumption);

                if (c.StatusEffects.HasStatusEffect(RangedEffectUnlimitedShot))
                {
                    recycleChance = 100;
                    if (hitOccured || c.GetMod(Mod.RETAIN_UNLIMITED_SHOT) <= 0) c.StatusEffects.DelStatusEffect(RangedEffectUnlimitedShot);
                }

                c.StatusEffects.DelStatusEffect(RangedEffectFlashyShot);
                c.StatusEffects.DelStatusEffect(RangedEffectStealthShot);

                if (ammo != null)
                {
                    bool recycleProc = rng.Next(100) < recycleChance;
                    if (!recycleProc)
                    {
                        ++ammoConsumed;
                        TrackArrowUsageForScavenge(c, ammo);
                        if (ammoQuantity == i) hitCount = i;
                    }
                }
            }

            // We hit the target at least once
            if (hitOccured)
            {
                if (isChar && r.MessageId != MsgRangedCrit)
                {
                    int distancePenalty = WsCallGlobalInt("xi.combat.ranged.attackDistancePenalty", c, target);
                    r.MessageId = distancePenalty == 0 ? MsgRangedPummels : distancePenalty <= 15 ? MsgRangedSquarely : MsgRangedHit;
                }

                // any misses with barrage/sange cause remaining shots to miss, meaning we must check Action.reaction
                if (r.Resolution != 0 && (isBarrage || isSange)) r.Resolution = 0;

                if (isChar && slot == CharStats.SlotRanged) totalDamage = (int)RangedDamageMultiplier(c, item, (uint)totalDamage, st.RapidShot, true);

                int taken = RangedTakePhysicalDamage(b, target, totalDamage, slot, realHits, now);
                RecordDamage(r, WsAttackPhysical, taken, target, wasCritical);

                if (wasCritical && target is MobEntity m)
                {
                    TriggerListener(m, "CRITICAL_TAKE", m, b);
                    CallMobHook(m, "onCriticalHit", m, b);
                }

                // Absorb message
                if (r.Param < 0)
                {
                    r.Param = -r.Param;
                    r.MessageId = MsgRangedAbsorbs;
                }
                // the additional effects: after the damage taken (or a sleep would not hold), the ammo's, else the
                // ranged weapon's (a boomerang's or a chakram's); none on the dead
                if (isChar)
                {
                    var aeItem = ammo != null ? ammo.Equip : item?.Equip;
                    if (aeItem != null && target.Hp > 0)
                    {
                        bool hasGlobal = CharStats.ScaledItemModifier(c, aeItem, Mod.ITEM_ADDEFFECT_TYPE) > 0;
                        bool hasScripted = CharStats.ScaledItemModifier(c, aeItem, Mod.ITEM_ADDEFFECT_SCRIPTED) > 0;
                        if (hasGlobal && hasScripted)
                            log_($"Item '{LookupItem(aeItem.ItemId)?.Name}' has misconfigured additional effect data with both item script and add effect global configured");
                        if (!(hasGlobal && AdditionalEffectAttack(c, target, aeItem, r, totalDamage) && r.AdditionalEffect != 0) && hasScripted)
                            OnItemAdditionalEffect(c, target, aeItem, r, totalDamage);
                    }
                    if (r.AddEffectMessage == MsgAddEffectDamage && r.AddEffectParam < 0)
                    {
                        r.AddEffectParam = -r.AddEffectParam;
                        r.AddEffectMessage = MsgAddEffectRecoversHp;
                    }
                }
            }
            else if (shadowsTaken > 0)
            {
                r.MessageId = MsgRangedShadowAbsorb;
                r.Resolution = 1;
                r.Param = shadowsTaken;
            }

            // Barrage/Sange: override message to display as ability; on a full miss the miss message stays
            if ((isBarrage || isSange) && hitOccured) r.MessageId = isSange ? MsgRangedUsesSange : MsgRangedUsesBarrage;

            if (isBarrage) b.StatusEffects.DelStatusEffectSilent(RangedEffectBarrage);
            if (isSange) c.StatusEffects.DelStatusEffectSilent(RangedEffectSange);

            if (!isChar)
            {
                // Mob or trust: the target was attacked now (CBattleEntity::LastAttacked, only a trust's is read)
                b.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Detectable);
                if (target is TrustEntity attacked) attacked.LastAttackedMs = now;
                ProcessActionEffectFlags(b, action);
                return;
            }

            if (target is MobEntity claimed) ClaimMob(claimed, c, now);

            RemoveAmmo(c, ammoConsumed);

            if (c.GetMod(Mod.RETAIN_CAMOUFLAGE) > 0)
            {
                short retainChance = 40;
                const int halfAllowance = 25 / 2; // rotAllowance 25
                float distanceToTarget = Distance(c, target);
                float meleeRange = HitboxSize(target) + 2.0f + HitboxSize(target); // PTarget->GetMeleeRange(PTarget)
                int facingDiff = Math.Abs(FacingAngle(PosOf(target), PosOf(c)));

                if (isBarrage)
                {
                    retainChance = 0;
                }
                else if (facingDiff > 128 - halfAllowance) // behind
                {
                    if (distanceToTarget > meleeRange + .6) retainChance = 100;
                    else if (distanceToTarget > meleeRange + .1) retainChance = (short)(retainChance + 1.6 * distanceToTarget);
                    else retainChance = 0;
                }
                else if (facingDiff > 64 - halfAllowance && facingDiff < 64 + halfAllowance) // beside
                {
                    if (distanceToTarget > meleeRange + 5) retainChance = 100;
                    else if (distanceToTarget > meleeRange + 3.3) retainChance = (short)(retainChance + 1.6 * distanceToTarget);
                    else retainChance = 0;
                }
                else
                {
                    if (distanceToTarget > meleeRange + 8.1) retainChance = 100;
                    else if (distanceToTarget > meleeRange + 7.1) retainChance = (short)(retainChance + 1.6 * distanceToTarget);
                    else retainChance = 0;
                }

                if (rng.Next(100) > retainChance)
                {
                    c.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Detectable);
                }
                else
                {
                    c.StatusEffects.DelStatusEffect(EffectId.Sneak);
                    c.StatusEffects.DelStatusEffect(RangedEffectDeodorize);
                    c.StatusEffects.DelStatusEffect(EffectId.Illusion);
                }
            }
            else
            {
                // Camouflage not up, so remove all detectable status effects
                c.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Detectable);
            }
            ProcessActionEffectFlags(c, action);
        }

        // charutils::TrackArrowUsageForScavenge: ArrowsUsed holds the ammo's id x 10000 plus the count (held at 1980),
        // started again for other ammo
        private static void TrackArrowUsageForScavenge(CharData c, WeaponInfo ammo)
        {
            uint id = ammo.Equip?.ItemId ?? 0;
            c.LocalVars.TryGetValue("ArrowsUsed", out uint used);
            if (used == 0 || used / 10000 != id) c.LocalVars["ArrowsUsed"] = id * 10000 + 1;
            else if (used % 10000 < 1980) c.LocalVars["ArrowsUsed"] = used + 1;
        }

        // battleutils::TakePhysicalDamage, PHYSICAL_ATTACK_TYPE::RANGED from a ranged attack: not blocked, not a
        // counter, TP to the victim; Formless Strikes' special damage, bind breaking and Scarlet Delirium. A mob's
        // ranged weapon deals no damage type.
        private int RangedTakePhysicalDamage(BattleEntity attacker, BattleEntity defender, int damage, int slot, int tpMultiplier, long now)
        {
            var weapon = attacker is CharData wc ? CharStats.WeaponIn(wc, slot) : null;
            bool giveTpToAttacker = !attacker.StatusEffects.HasStatusEffect(WsEffectMeikyoShisui);

            bool formless = Melee.IsFormless(attacker, false);
            byte attackType = formless ? WsAttackSpecial : WsAttackRanged;
            uint damageType = formless ? DamageType.None : weapon?.DmgType ?? DamageType.None;
            if (formless) damage = MeleeFor().FormlessDmgTaken(attacker, defender, damage);
            else
            {
                damage = WsDmgTaken(defender, damage, true);

                Mod? sdt = damageType switch
                {
                    DamageType.Piercing => Mod.PIERCE_SDT,
                    DamageType.Slashing => Mod.SLASH_SDT,
                    DamageType.Blunt => Mod.IMPACT_SDT,
                    DamageType.HandToHand => Mod.HTH_SDT,
                    _ => null,
                };
                if (sdt is Mod sdtMod) damage = (int)(damage * (1 + defender.GetMod(sdtMod) / 10000.0f));
            }

            if (damage > 0)
            {
                damage = Math.Max(damage - defender.GetMod(Mod.PHALANX), 0);
                damage = WsHandleStoneskin(defender, damage, attackType);
                // HandleAfflatusMiseryDamage
                if (defender.StatusEffects.HasStatusEffect(EffectAfflatusMisery)) defender.SetModifier(Mod.AFFLATUS_MISERY, (short)damage);
            }
            damage = Math.Clamp(damage, -99999, 99999);
            damage = MeleeFor().DamageCap(defender, damage);
            Melee.HandleScarletDelirium(defender, damage);

            int corrected = BattleTakeDamage(defender, damage, attacker, attackType, (byte)damageType);
            if (damage < 0) damage = -corrected;

            // Only claim a mob and if the allegiance is not PLAYER
            if (defender is MobEntity claimed && claimed.Allegiance != EnmityContainer.AllegianceOf(attacker) && attacker is CharData claimer)
                ClaimMob(claimed, claimer, now);

            if (damage > 0)
            {
                defender.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Damage);
                BindBreakCheck(defender);
                if (defender is MobEntity dm)
                {
                    dm.Enmity.UpdateEnmityFromDamage(attacker, damage);
                    if (dm.HiPcLvl < attacker.MLvl) dm.HiPcLvl = attacker.MLvl;
                }
                else if (defender is CharData && attacker is MobEntity am)
                {
                    am.Enmity.UpdateEnmityFromAttack(defender, damage);
                }
                // a ranged attack does not try to interrupt a spell

                if (giveTpToAttacker)
                {
                    short attackerTpReturn = (short)CalculateTpFromDamageDealt(attacker, slot);
                    WsAddTp(attacker, (short)(tpMultiplier * attackerTpReturn));
                }

                int delay = attacker is CharData ? BaseRangedDelay(attacker) : Melee.BaseDelay(attacker);
                short defenderTpReturn = (short)CalculateTpFromDamageTaken(attacker, defender, damage, delay);
                WsAddTp(defender, (short)(tpMultiplier * defenderTpReturn));
            }
            else if (defender is MobEntity dm)
            {
                dm.Enmity.UpdateEnmityFromDamage(attacker, 0);
            }
            return damage;
        }

        // attackutils::CheckForDamageMultiplier for PHYSICAL_ATTACK_TYPE::RANGED and RAPID_SHOT
        private uint RangedDamageMultiplier(CharData c, WeaponInfo weapon, uint damage, bool rapidShot, bool allowProc) =>
            PhysicalDamageMultiplier(c, weapon, damage, rapidShot ? PhysicalAttackType.RapidShot : PhysicalAttackType.Ranged,
                CharStats.SlotRanged, allowProc, MobRng);

        // ---- the ranged battleutils ------------------------------------------------------------------------------------

        // battleutils::GetRangedHitRate, with any accuracy bonus (a Daken throw's)
        private int RangedHitRate(BattleEntity attacker, BattleEntity defender, int accBonus = 0)
        {
            // Check to see if distance is greater than 25 and force hitrate to be 0
            if (Distance(attacker, defender) > 25) return 0;
            double luaHitRate = RangedCallGlobal("xi.combat.physicalHitRate.getRangedHitRate", 0.0, () => lua_.ToNumber(-1),
                attacker, defender, accBonus, false);
            return (byte)(luaHitRate * 100.0);
        }

        // battleutils::GetRangedDamageRatio, with any bonus ranged attack (a Daken throw's); a mob's ranged weapon has no
        // skill
        private float RangedDamageRatio(BattleEntity attacker, BattleEntity defender, bool isCritical, int bonusRangedAttack = 0)
        {
            float pDif = 1.0f;
            int skill = Skill.None;
            if (attacker is CharData ac)
            {
                var weapon = ac.RangedWeapon ?? ac.AmmoWeapon; // no ranged weapon: the ammo slot, for throwing
                if (weapon == null)
                {
                    log_("battleutils::GetRangedDamageRatio(): No ranged weapon or ammo");
                    return pDif;
                }
                skill = weapon.Skill;
            }

            bool? levelCorrection = RangedCallGlobal<bool?>("xi.data.levelCorrection.isLevelCorrectedZone", null, () => lua_.ToBoolean(-1), attacker);
            if (levelCorrection == null) return pDif;
            pDif = RangedCallGlobal("xi.combat.physical.calculateRangedPDIF", pDif, () => (float)lua_.ToNumber(-1),
                attacker, defender, skill, 1.0, isCritical, levelCorrection.Value, false, 0.0, false, bonusRangedAttack);
            return Math.Max(pDif, 0.0f);
        }

        // battleutils::GetRangedCritHitRate
        private static int RangedCritHitRate(BattleEntity attacker, BattleEntity defender)
        {
            int critHitRate = 5;
            // apply merit mods and traits
            if (attacker is CharData ac) critHitRate += ac.Merits?.Value(ac, RangedMeritCritHitRate) ?? 0;
            if (defender is CharData dc) critHitRate -= dc.Merits?.Value(dc, RangedMeritEnemyCritRate) ?? 0;

            // Innin crit rate bonus from behind the target (behind(attacker, defender, 64))
            if (attacker.StatusEffects.GetStatusEffect(RangedEffectInnin) is { } innin && Math.Abs(FacingAngle(PosOf(defender), PosOf(attacker))) > 128 - 32)
                critHitRate += innin.Power;
            // Yonin enemy crit rate reduction while in front of the target (infront(defender, attacker, 64))
            if (defender.StatusEffects.GetStatusEffect(RangedEffectYonin) is { } yonin && Math.Abs(FacingAngle(PosOf(attacker), PosOf(defender))) < 32)
                critHitRate -= yonin.Power;

            // Mighty Strikes, since ranged attacks do not get the crit bonus
            critHitRate += attacker.GetMod(Mod.CRITHITRATE) - (attacker.StatusEffects.HasStatusEffect(EffectId.MightyStrikes) ? 100 : 0);

            // GetAGICritBonus: only a positive dAGI counts
            critHitRate += Math.Max(0, Melee.Agi(attacker) - Melee.Agi(defender)) / 10;
            critHitRate -= defender.GetMod(Mod.CRITICAL_HIT_EVASION);
            return Math.Clamp(critHitRate, 0, 100);
        }

        // CBattleEntity::GetRangedWeaponDelay(false): the ranged weapon's base delay (or the ammo's when it alone deals
        // damage) in ms, with RANGED_DELAY and RANGED_DELAYP; a mob's ranged weapon has its own base delay
        private static short RangedWeaponDelay(BattleEntity b)
        {
            short delay = 0;
            if (b is CharData c)
            {
                var range = c.RangedWeapon;
                var ammo = c.AmmoWeapon;
                if (range != null && range.Damage != 0) delay = (short)range.Delay;
                else if (ammo != null && ammo.Damage != 0) delay = (short)ammo.Delay;
            }
            else if (b is MobEntity m && m.RangedDamage != 0)
            {
                delay = (short)m.RangedBaseDelay;
            }

            // multiple the base delays by 1000 so final delays are in ms; divide by 120 to convert the delays to actual times
            delay = (short)((delay - b.GetMod(Mod.RANGED_DELAY)) * 1000 / 120);
            // apply haste and delay reductions that don't affect tp
            delay = (short)(delay * ((100.0f + b.GetMod(Mod.RANGED_DELAYP)) / 100.0f));
            return delay;
        }

        // battleutils::GetRangedDelayReduction: Snapshot (capped at 70%) and Velocity Shot
        private static short RangedDelayReduction(BattleEntity b, short delay)
        {
            int snapshot = b.GetMod(Mod.SNAPSHOT);
            if (b is CharData c && CharStats.HasTrait(c, RangedTraitSnapshot)) snapshot += c.Merits?.Value(c, RangedMeritSnapshot) ?? 0;
            snapshot = Math.Min(snapshot, 70);

            int velocityShot = 0;
            if (b.StatusEffects.HasStatusEffect(RangedEffectVelocityShot)) velocityShot = 15 + b.GetMod(Mod.VELOCITY_SNAPSHOT_BONUS);

            return (short)(delay * ((100 - snapshot) / 100.0f) * ((100 - velocityShot) / 100.0f));
        }

        // battleutils::getBarrageShotCount: a char's limited to bows and guns and by its ammo
        private static byte RangedBarrageShotCount(BattleEntity b)
        {
            int lvl = b.MJob == JobRng ? b.MLvl : b.SLvl;
            byte shotCount;
            if (lvl < 30) return 0;
            else if (lvl < 50) shotCount = 3;
            else if (lvl < 75) shotCount = 4;
            else if (lvl < 90) shotCount = 5;
            else if (lvl < 99) shotCount = 6;
            else shotCount = 7;

            shotCount = (byte)(shotCount + b.GetMod(Mod.BARRAGE_COUNT));
            if (b is not CharData c) return shotCount;

            // only archery + marksmanship can use barrage
            var item = c.RangedWeapon;
            if (item != null && item.Skill != Skill.Archery && item.Skill != Skill.Marksmanship) return 0;

            // make sure we have enough ammo for all these shots (one ammo is already consumed by the first shot)
            if (c.AmmoWeapon != null && CharStats.EquippedItem(c, CharStats.SlotAmmo) is { } ammo && ammo.Quantity < shotCount + 1u)
                shotCount = (byte)(ammo.Quantity - 1);
            return shotCount;
        }

        // luautils::callGlobal<T> (and the sol calls of GetRangedDamageRatio): the fallback on a missing function or an
        // error
        private T RangedCallGlobal<T>(string path, T fallback, Func<T> read, params object[] args)
        {
            if (lua_ == null) return fallback;
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (lua_.IsNil(-1))
                {
                    log_($"luautils::callGlobal: {path}: Function not found");
                    return fallback;
                }
                foreach (var a in args) lua_.Push(a);
                lua_.Call(args.Length, 1);
                return read();
            }
            catch (LuaException e)
            {
                log_($"luautils::callGlobal: {path}: {e.Message}");
                return fallback;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }
    }
}
