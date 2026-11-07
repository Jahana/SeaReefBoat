using System;
using System.Collections.Generic;
using XI.Common;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // Melee combat: CPlayerController::Engage / Disengage / ChangeTarget, CAttackState, CCharEntity::CanAttack,
    // CBattleEntity::OnAttack (one round of swings as a 0x028 basic attack, each through MeleeSwing with the defender's
    // reactions), battleutils::TakePhysicalDamage and ClaimMob, and a mob's death up to its defeat message. Engaging, a
    // new target and disengaging recheck the char's latents (CLatentEffectContainer::CheckLatentsWeaponDraw /
    // CheckLatentsTargetChange).
    // Paralysis or intimidation stops a round (the claim stays): ActionInterrupts::AttackParalyzed / AttackIntimidated.
    // A target lost mid fight hands over to a fighting mob in front within 10 yalms when /autotarget is on
    // (CAttackState::UpdateTarget).
    public sealed partial class ZoneHost
    {
        private Melee melee_;
        private Melee MeleeFor() => melee_ ??= new Melee(loader_.Stats, ChatCfg, () => MobRng)
        {
            TrickAttackCharOf = TrickAttackChar,
            CoverAbilityUserOf = CoverAbilityUser,
            SevereDamage = HandleSevereDamage,
            AddCharHp = AddHp,
            RangedHitRateOf = RangedHitRate,
            RangedDamageRatioOf = RangedDamageRatio,
            RangedBaseDamageOf = (attacker, defender) =>
                RangedWeaponDmg(attacker) + WsCallGlobalInt("xi.combat.physical.calculateRangedStatFactor", attacker, defender),
            RemoveAmmoOf = RemoveAmmo,
            HitRateModifiersOf = HitRateModifiers,
            SignetBonusOf = SignetBonus,
        };

        // the zone's melee formulas (drawing from MobRng), for the verify suites
        public Melee MeleeEngine => MeleeFor();

        private const ushort EffectFlash = 156, EffectSignetBonus = 253, EffectYoninBonus = 420, EffectInninBonus = 421;
        private const ushort MeritAmbushBonus = 2374, MeritClosedPosition = 3206;
        private const int TraitAmbush = 76, JpYoninEffect = 0x1A1, JpInninEffect = 0x1A3;
        private const byte RegionLimbus = 27;

        // xi.combat.physicalHitRate.getHitRateModifiers(attacker, target, false, false) for a melee swing: Innin's
        // potency from behind the target (with the target's Innin job points, as the script reads them), the Closed
        // Position merits of a char facing the other, the Ambush merits from behind, the attacker's own Yonin while
        // facing the target (an evasion bonus, with the target's Yonin job points), Flash's penalty (0.03 a millisecond
        // left)
        private (int Acc, int Eva) HitRateModifiers(BattleEntity attacker, BattleEntity target)
        {
            int acc = 0, eva = 0;
            var a = PosOf(attacker);
            var t = PosOf(target);
            bool attackerBehind = Math.Abs(FacingAngle(t, a)) > 128 - 23 / 2; // attacker:isBehind(target, 23)
            if (attacker.StatusEffects.GetStatusEffect(EffectInninBonus) is { } innin && attackerBehind)
                acc += innin.Power + (target is CharData ti ? JobPointUtil.Value(ti, JpInninEffect) : 0);
            if (attacker is CharData ac && Math.Abs(FacingAngle(a, t)) < 64 / 2)
                acc += ac.Merits?.Value(ac, MeritClosedPosition) ?? 0;
            if (attacker.HasTrait(TraitAmbush) && attackerBehind && attacker is CharData am)
                acc += am.Merits?.Value(am, MeritAmbushBonus) ?? 0;
            if (attacker.StatusEffects.GetStatusEffect(EffectYoninBonus) is { } yonin && Math.Abs(FacingAngle(a, t)) < 64 / 2)
                eva += yonin.Power + 2 * (target is CharData ty ? JobPointUtil.Value(ty, JpYoninEffect) : 0);
            if (target is CharData tc && Math.Abs(FacingAngle(t, a)) < 64 / 2)
                eva += tc.Merits?.Value(tc, MeritClosedPosition) ?? 0;
            if (attacker.StatusEffects.GetStatusEffect(EffectFlash) is { } flash)
            {
                long remaining = flash.DurationMs > 0 ? Math.Max(flash.StartMs - NowMs() + flash.DurationMs, 0) : 0;
                acc -= (int)Math.Floor(remaining * 0.03);
            }
            return (acc, eva);
        }

        // shouldApplySignetBonus: a char under Signet, auto-attacking a mob (not an NM) that checks an even match or
        // weaker, in a region up to Limbus
        private bool SignetBonus(BattleEntity attacker, BattleEntity target) =>
            target is CharData c && c.StatusEffects.HasStatusEffect(EffectSignetBonus) &&
            attacker is MobEntity mob and not PetEntity and not TrustEntity && !mob.IsNotorious &&
            CheckMob(c.MLvl, mob) <= MobEvenMatch && ZoneRegions.Of(c.Zone) <= RegionLimbus &&
            c.BattleTarget == attacker;

        private const ushort EffectCover = 114;

        // battleutils::GetCoverAbilityUser: the target's party member that covers it (its COVER_ABILITY_TARGET local
        // var naming the target, under Cover, alive), when it stands in the attacker's melee range, at least 0.5 yalms
        // off it, nearer than the target and in line with it
        private BattleEntity CoverAbilityUser(BattleEntity coverTarget, BattleEntity attacker)
        {
            if (coverTarget is not CharData tc || tc.Party == null) return null;
            CharData user = null;
            foreach (var member in tc.Party.Members)
            {
                if (member.LocalVars.TryGetValue("COVER_ABILITY_TARGET", out uint covered) && covered == tc.Id &&
                    member.StatusEffects.HasStatusEffect(EffectCover) && member.IsAlive)
                {
                    user = member;
                    break;
                }
            }
            if (user == null) return null;
            // the same names as the trick attack check
            byte angleTaMob = WorldAngle(attacker.X, attacker.Z, attacker.Rotation, user.X, user.Z);
            float distTaMob = Distance(user, attacker);
            byte targetAngle = WorldAngle(attacker.X, attacker.Z, attacker.Rotation, tc.X, tc.Z);
            if (distTaMob <= HitboxOf(attacker) + 2.0f + HitboxOf(user) && distTaMob >= Melee.WorldAngleMinDistance &&
                distTaMob < Distance(tc, attacker) && Math.Abs(Melee.AngleDifference(angleTaMob, targetAngle)) <= Melee.WorldAngleMaxDeviance)
                return user;
            return null;
        }

        // battleutils::getAvailableTrickAttackChar: the nearest living alliance member or member's trust (in this zone;
        // the fellow is commented out in the C++) between the TA user and the mob, at least 0.5 from the mob and in line
        // with the user
        private BattleEntity TrickAttackChar(BattleEntity taUser, BattleEntity mob)
        {
            if (!taUser.StatusEffects.HasStatusEffect(EffectId.TrickAttack)) return null;
            byte angleTaMob = WorldAngle(mob.X, mob.Z, mob.Rotation, taUser.X, taUser.Z);
            float distTaMob = Distance(taUser, mob);
            var targets = new List<(float Dist, BattleEntity Member)>();
            if (taUser is CharData tc && tc.Party != null)
            {
                foreach (var member in ForAlliance(tc))
                {
                    if (!SameArea(member, tc)) continue;
                    float dist = Distance(member, mob);
                    // require closer target not be closer than .5 yalms to mob
                    if (dist >= Melee.WorldAngleMinDistance && dist < distTaMob) targets.Add((dist, member));
                    foreach (var trust in member.PTrusts)
                    {
                        dist = Distance(trust, mob);
                        if (dist >= Melee.WorldAngleMinDistance && dist < distTaMob) targets.Add((dist, trust));
                    }
                }
            }
            targets.Sort((a, b) => a.Dist != b.Dist ? a.Dist.CompareTo(b.Dist) : a.Member.Id.CompareTo(b.Member.Id));
            foreach (var (_, member) in targets)
            {
                if (member == taUser || BattleDead(member)) continue; // can't TA self; the dead aren't TA-able
                // battleutils::areInLine
                byte memberAngle = WorldAngle(mob.X, mob.Z, mob.Rotation, member.X, member.Z);
                if (Math.Abs(Melee.AngleDifference(angleTaMob, memberAngle)) <= Melee.WorldAngleMaxDeviance) return member;
            }
            return null;
        }

        // msg_basic.h, the ones melee sends
        private const ushort MsgHit = 1, MsgCannotSee = 5, MsgDefeatsTarget = 6, MsgMiss = 15, MsgCallForHelp = 19,
            MsgFallsToGround = 20, MsgCannotCallForHelp = 22, MsgTargetOutOfRange = 4, MsgAlreadyClaimed = 12,
            MsgLoseSight = 36, MsgCrit = 67, MsgTooFarAway = 78, MsgAbsorb = 373;

        // xi::ClaimType (MobMod.ClaimType)
        private const int ClaimNonExclusive = 1, ClaimUnclaimable = 2;

        private static float Distance(BattleEntity a, BattleEntity b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        // facing(A, B, 64): B lies within 32 of A's rotation
        private static bool Facing(BattleEntity a, BattleEntity b)
        {
            int diff = WorldAngle(a.X, a.Z, a.Rotation, b.X, b.Z) - a.Rotation;
            if (diff > 128) diff -= 256;
            else if (diff < -128) diff += 256;
            return Math.Abs(diff) < 32;
        }

        // GetEntity(targid, TYPE_MOB) in the char's zone
        private MobEntity MobByTargId(CharData c, ushort targid)
        {
            foreach (var m in AreaOf(c).Mobs)
                if (m.TargId == targid) return m;
            return null;
        }

        // IsValidTarget(TARGET_ENEMY): a spawned, living mob of another allegiance in the char's zone
        // or a living char of the char's area it may harm (CCharEntity::ValidTarget: a belligerent, or a char of another
        // team or nation)
        private static bool ValidEnemy(CharData c, BattleEntity target) => target switch
        {
            MobEntity m => m.IsSpawned && SameArea(m, c) && m.IsAlive && !m.IsDead && m.Allegiance != CharData.AllegiancePlayer,
            CharData pc => pc != c && SameArea(pc, c) && !BattleDead(pc) && CharValidTarget(c, pc, TargetEnemy),
            _ => false,
        };

        // isDead: out of HP, or a mob or char already in its death state
        private static bool BattleDead(BattleEntity b) => !b.IsAlive || b is MobEntity { IsDead: true } || b is CharData { DeathAtMs: not 0 };

        // GetEntity(targid, TYPE_MOB | TYPE_PC) for an engage: a mob, else a char of the char's area
        private BattleEntity EngageTargetByTargId(CharData c, ushort targid) => (BattleEntity)MobByTargId(c, targid) ?? CharByTargId(c, targid);

        // GP_SERV_COMMAND_ASSIST (0x058): the char's battle target, 0 for none
        private static BasicPacket AssistPacket(CharData c, BattleEntity target)
        {
            var w = new PacketWriter(0x058, 0x10);
            w.U32(0x04, c.Id);
            w.U32(0x08, target?.Id ?? 0);
            w.U16(0x0C, c.TargId);
            return w.Packet;
        }

        // battleutils::assistTarget: lock on to what the char or mob at that index is fighting
        private void AssistTarget(CharData c, ushort targid)
        {
            BattleEntity target = EntityAt(c, targid) switch
            {
                CharData pc => pc.BattleTarget,
                MobEntity m and not PetEntity and not TrustEntity => m.BattleTarget,
                _ => null,
            };
            if (target != null) PushTo(c, AssistPacket(c, target));
        }

        private void PushBattleError(CharData c, BattleEntity target, ushort msg) =>
            PushTo(c, SelfPackets.BattleMessage(c.Id, c.TargId, target.Id, target.TargId, 0, 0, msg));

        // CPlayerController::Engage: an invalid target is ignored; too far, or before the engage lockout runs out,
        // is an error. An engage (or a new target) draws the weapon's latents and points the client at the target. The
        // target may be a mob or a char the char may harm (PvP).
        private void Engage(CharData c, ushort targid)
        {
            var target = EngageTargetByTargId(c, targid);
            if (target == null || !ValidEnemy(c, target)) return;
            if (Distance(c, target) >= 30)
            {
                PushBattleError(c, target, MsgTooFarAway);
                return;
            }
            long now = NowMs();
            if (now < c.EngageLockedUntilMs)
            {
                PushBattleError(c, target, MsgWaitLonger);
                return;
            }
            if (InternalEngage(c, target, now))
            {
                c.Latents.CheckLatentsWeaponDraw(true);
                PushTo(c, AssistPacket(c, target));
            }
        }

        // CAIContainer::Internal_Engage: an engaged char just changes target (false when it is the same one); otherwise
        // the attack state starts, its first swing 3 s off, and CCharEntity::OnEngage draws the weapon and rechecks the
        // target's latents
        private bool InternalEngage(CharData c, BattleEntity target, long now)
        {
            if (c.AttackTarget != null)
            {
                if (c.BattleTarget == target) return false;
                c.BattleTarget = target;
                return true;
            }
            c.BattleTarget = target;
            c.AttackTarget = target;
            c.BattleStartMs = now;
            c.SwingDelayMs = MeleeFor().WeaponDelay(c, false);
            c.SwingAtMs = now + 3000;
            c.Animation = Anim.Attack;
            c.UpdateMask |= CharStats.UpdateHp;
            c.Latents.CheckLatentsTargetChange();
            c.History[HistoryBattlesFought]++;
            return true;
        }

        // CPlayerController::Disengage: the attack state sees no battle target on its next tick and ends
        private static void Disengage(CharData c) => c.BattleTarget = null;

        // CPlayerController::ChangeTarget: an engaged char (or one without a target) switches, anyone else engages
        private void ChangeTarget(CharData c, ushort targid)
        {
            if (c.AttackTarget != null || c.BattleTarget == null) c.BattleTarget = EngageTargetByTargId(c, targid);
            else Engage(c, targid);
        }

        // CCharEntity::OnDisengage: the engage lockout, the claim handed on, the weapon put away and its latents with it
        private void EndAttackState(CharData c, long now)
        {
            long remaining = Math.Max(0, c.SwingAtMs - now);
            long max = Math.Max(1500, c.SwingDelayMs - 3000);
            c.EngageLockedUntilMs = now + Math.Clamp(remaining, 1500, max);
            RelinquishClaim(c, now);
            c.AttackTarget = null;
            c.BattleTarget = null;
            if (c.Animation == Anim.Attack)
            {
                c.Animation = Anim.None;
                c.UpdateMask |= CharStats.UpdateHp;
            }
            c.Latents.CheckLatentsWeaponDraw(false);
            if (SessionOf(c) is { } s) SendUpdateMask(s, c);
        }

        // CAttackState::Update for a char: UpdateTarget, then a swing when it is due
        private void UpdateAttackState(MapSession s, CharData c, long now)
        {
            // the attack state is suspended under a weapon skill, monster skill, ranged attack, job ability or magic state
            if (c.AttackTarget == null || c.WsState != null || c.SkillState != null || c.RangedState != null || c.AbilityState != null || c.MagicState != null || c.InactiveState != null) return;
            var bt = c.BattleTarget;
            if (bt != null && !ValidEnemy(c, bt))
            {
                var lostTarget = bt;
                bt = HasAutoTargetEnabled(c) ? AutoTarget(s, c) : null;
                // target died: restart the swing timer at a full delay
                if (bt == null && BattleDead(lostTarget)) c.SwingAtMs = now + c.SwingDelayMs;
                c.BattleTarget = bt;
            }
            if (c.AttackTarget != bt && bt != null)
            {
                // CPlayerController::OnChangeTarget
                RelinquishClaim(c, now);
                Push(s, AssistPacket(c, bt));
                c.AttackTarget = bt;
                c.Latents.CheckLatentsTargetChange();
            }
            if (bt == null || !c.IsAlive)
            {
                EndAttackState(c, now);
                return;
            }
            if (now < c.SwingAtMs) return;

            if (CanAttack(c, bt, out ushort err))
            {
                c.SwingAtMs += c.SwingDelayMs;
                if (Attack(c, bt, now) is { } attack) PushCharInRange(c, Battle2Packets.Battle2(attack), true);
            }
            else
            {
                if (err != 0 && c.LastErrMsgMs + MeleeFor().WeaponDelay(c, false) < now)
                {
                    c.LastErrMsgMs = now;
                    PushBattleError(c, bt, err);
                }
                if (c.BattleTarget == null) EndAttackState(c, now);
            }
            if (c.SwingAtMs < now) c.SwingAtMs = now;
            SendUpdateMask(s, c);
        }

        // CCharEntity::hasAutoTargetEnabled: the /autotarget setting (playerConfig.AutoTargetOffFlg) is not off
        private static bool HasAutoTargetEnabled(CharData c) => (c.Settings & 1u << 14) == 0;

        // CAttackState::UpdateTarget's auto-target: the first mob the char has spawned (by id) that is fighting, in
        // front of the char within 10 yalms and a valid enemy target; the client is told with an ASSIST packet
        private BattleEntity AutoTarget(MapSession s, CharData c)
        {
            var zone = AreaOf(c);
            var ids = new List<uint>(c.SpawnMobList);
            ids.Sort();
            foreach (uint id in ids)
            {
                if (!zone.MobById.TryGetValue(id, out var m)) continue;
                if (m.Animation == Anim.Attack && Facing(c, m) && Distance(c, m) <= 10 &&
                    AbilityValidTarget(c, m, TargetEnemy, out _) != null)
                {
                    Push(s, AssistPacket(c, m));
                    return m;
                }
            }
            return null;
        }

        // CCharEntity::CanAttack (a char is always its own to hit)
        private bool CanAttack(CharData c, BattleEntity target, out ushort err)
        {
            err = 0;
            if (IsUntargetable(target)) return false;
            float dist = Distance(c, target);
            if (target is MobEntity tm && !IsMobOwner(c, tm))
            {
                err = MsgAlreadyClaimed;
                Disengage(c);
                return false;
            }
            if (dist > 30)
            {
                err = MsgLoseSight;
                Disengage(c);
                return false;
            }
            if (!Facing(c, target))
            {
                err = MsgCannotSee;
                return false;
            }
            if (dist > c.ModelHitboxSize10 / 10.0f + 2.0f + HitboxOf(target))
            {
                err = MsgTargetOutOfRange;
                return false;
            }
            return true;
        }

        // CAttack::CheckCover: a living cover user takes the swing
        private static bool CheckCover(Melee.Swing swing)
        {
            swing.Covered = swing.Round?.CoverUser is { IsAlive: true };
            return swing.Covered;
        }

        // CBattleEntity::OnAttack's IsParalyzed / IsIntimidated: ActionInterrupts::AttackParalyzed / AttackIntimidated,
        // a magic finish on the target with the message, to the attacker and those around; no round goes out. Unlike
        // C++, a die is only rolled when there is a chance (the odds are the same; the rounds' dice stay as they were).
        private bool AttackInterrupted(BattleEntity attacker, BattleEntity target)
        {
            bool paralyzed = attacker.GetMod(Mod.PARALYZE) > 0 && IsParalyzed(attacker);
            bool intimidated = !paralyzed && attacker != target && IntimidationChance(attacker, target) > 0 && IsIntimidated(attacker, target);
            ushort msg = paralyzed ? MsgIsParalyzed2 : intimidated ? MsgIsIntimidated : (ushort)0;
            if (msg == 0) return false;
            var finish = new ActionPacket { ActorId = attacker.Id, ActionType = ActionCategory.MagicFinish };
            var r = finish.AddTarget(target.Id).AddResult();
            r.Animation = AnimSkillInterrupt;
            r.MessageId = msg;
            if (attacker is CharData c) PushCharInRange(c, Battle2Packets.Battle2(finish), true);
            else if (attacker is MobEntity m) PushMobInRange(m, Battle2Packets.Battle2(finish));
            return true;
        }

        // CBattleEntity::OnAttack: a round of swings while both stand, at most 8 results; none (null) when paralysis
        // or intimidation stops it
        private ActionPacket Attack(CharData c, BattleEntity target, long now)
        {
            c.LastAttackMs = now;
            var melee = MeleeFor();
            if (target is MobEntity mobTarget) ClaimMob(mobTarget, c, now);
            if (AttackInterrupted(c, target)) return null;
            var action = new ActionPacket { ActorId = c.Id, ActionType = ActionCategory.BasicAttack };
            var t = action.AddTarget(target.Id);
            var swings = melee.BuildRound(c);
            bool first = true;
            while (swings.Count > 0 && target.IsAlive && c.IsAlive)
            {
                var swing = swings[0];
                var r = t.AddResult();
                r.Animation = Melee.Animation(swing);
                if (CheckCover(swing))
                {
                    target = swing.Round.CoverUser;
                    t.ActorId = target.Id;
                }
                MeleeSwing(c, target, r, swing, now);
                TryZanshin(c, swing, r, swings);
                // a Daken throw under Sange spends the shuriken
                if (swing.Type == Melee.AttackType.Daken && c.StatusEffects.HasStatusEffect(RangedEffectSange) &&
                    c.AmmoWeapon is { Skill: Skill.Throwing, SubSkill: 3 })
                    RemoveAmmo(c, 1);
                if (first)
                {
                    first = false;
                    c.StatusEffects.DelStatusEffectSilent(EffectId.SneakAttack);
                    c.StatusEffects.DelStatusEffectSilent(EffectId.TrickAttack);
                }
                swings.RemoveAt(0);
                if (t.Results.Count >= 8) break;
            }
            TriggerListener(c, "ATTACK", c, target, action);
            TriggerListener(target, "ATTACKED", target, c, action);
            lua_?.Forget(action);
            c.StatusEffects.DelStatusEffectSilent(EffectId.Boost);
            c.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Detectable, EffectNotice.Silent);
            ProcessActionEffectFlags(c, action);
            action.Normalize();
            return action;
        }

        // battleutils::TakePhysicalDamage for a char hitting a mob: the damage taken (a block's share, a counter's always
        // hand-to-hand), the claim, enmity and TP (none to a char under Meikyo Shisui, none to a counter's dealer)
        private int TakePhysicalDamage(CharData attacker, MobEntity defender, int damage, int slot, long now, bool isBlocked = false,
            bool giveTpToAttacker = true, bool isCounter = false, BattleEntity taChar = null, bool isZanshin = false)
        {
            var melee = MeleeFor();
            giveTpToAttacker = giveTpToAttacker && !attacker.StatusEffects.HasStatusEffect(EffectMeikyoShisui);
            // a Daken throw (the ammo slot): ranged, no spell interrupt, the ranged TP return and none to the victim
            bool isRanged = slot == CharStats.SlotAmmo || slot == CharStats.SlotRanged;
            int dmg = melee.MeleeDamageTaken(attacker, defender, damage, slot, isBlocked, isCounter, tp => AddMobTp(defender, (short)tp),
                addMp: mp => BattleAddMp(defender, mp), giveTpToAttacker: giveTpToAttacker);
            dmg = PhysicalTakeDamage(defender, attacker, dmg, slot, isCounter);
            if (defender.Allegiance != CharData.AllegiancePlayer) ClaimMob(defender, attacker, now);
            if (dmg > 0)
            {
                defender.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Damage);
                BindBreakCheck(defender);
                if (!isRanged) TryHitInterrupt(defender, attacker);
                defender.Enmity.UpdateEnmityFromDamage(taChar ?? attacker, dmg);
                if (giveTpToAttacker)
                    AddTp(attacker, (short)(isRanged ? CalculateTpFromDamageDealt(attacker, slot) : Melee.SingleMeleeHitTpReturn(attacker, isZanshin)));
                if (!isRanged) AddMobTp(defender, (short)Melee.TpGainOnPhysicalDamage(attacker, defender, dmg, Melee.BaseDelay(attacker)));
            }
            else defender.Enmity.UpdateEnmityFromDamage(taChar ?? attacker, 0);
            return dmg;
        }

        // CBattleEntity::takeDamage for a mob: an absorb comes back negative; a mob at 0 dies on its PostTick
        // CBattleEntity::takeDamage for a melee blow (TakePhysicalDamage): physical with the weapon's damage type, special
        // and typeless under Formless Strikes; an absorb comes back as the HP healed
        private int PhysicalTakeDamage(BattleEntity defender, BattleEntity attacker, int dmg, int slot, bool isCounter)
        {
            bool formless = Melee.IsFormless(attacker, isCounter);
            byte damageType = formless ? (byte)0 : attacker is CharData c ? (byte)(CharStats.WeaponIn(c, slot)?.DmgType ?? 0) : (byte)0;
            bool ranged = slot == CharStats.SlotAmmo || slot == CharStats.SlotRanged;
            int corrected = BattleTakeDamage(defender, dmg, attacker, formless ? WsAttackSpecial : ranged ? WsAttackRanged : WsAttackPhysical, damageType);
            if (dmg < 0) dmg = -corrected;
            return dmg;
        }

        // battleutils::BindBreakCheck: a blow that lands breaks bind 95% of the time
        private void BindBreakCheck(BattleEntity defender)
        {
            if (defender.StatusEffects.HasStatusEffect(EffectId.Bind) && 950 > MobRng.Next(1000))
                defender.StatusEffects.DelStatusEffect(EffectId.Bind);
        }

        private int MobTakeDamage(MobEntity m, BattleEntity attacker, int damage, long now)
        {
            m.LastAttackerId = attacker.Id;
            m.LastHitAtkType = AttackTypePhysical;
            int before = m.Hp;
            m.Hp = Math.Clamp(m.Hp - damage, 0, m.MaxHp);
            if (m.Hp != before) m.UpdateMask |= EntityUpdatePacket.UpdateHp;
            if (m.Hp == 0 && m.Unkillable) m.Hp = 1;
            if (damage < 0) damage = -(m.Hp - before);
            return damage;
        }

        // CBattleEntity::addHP for a mob
        private static int MobAddHp(MobEntity m, int hp)
        {
            if (m.Hp == 0 && hp < 0) return 0;
            int cap = Math.Clamp(m.Hp + hp, 0, m.MaxHp);
            hp = m.Hp - cap;
            m.Hp = cap;
            if (hp != 0) m.UpdateMask |= EntityUpdatePacket.UpdateHp;
            if (m.Hp == 0 && m.Unkillable) m.Hp = 1;
            return Math.Abs(hp);
        }

        // CBattleEntity::addTP for a mob: map.MOB_TP_MULTIPLIER, map.PET_TP_MULTIPLIER for a pet (a char's, or a mob with a
        // master), map.TRUST_TP_MULTIPLIER for a trust
        private void AddMobTp(MobEntity m, short tp)
        {
            if (tp > 0)
            {
                float reduce = m.GetMod(Mod.INHIBIT_TP) / 100.0f;
                tp = (short)(tp - tp * reduce);
                tp = (short)(tp * (m is TrustEntity ? ChatCfg.TrustTpMultiplier
                    : m is PetEntity || m.PMaster != null ? ChatCfg.PetTpMultiplier : ChatCfg.MobTpMultiplier));
            }
            if (tp != 0) m.UpdateMask |= EntityUpdatePacket.UpdateHp;
            m.Tp = Math.Clamp(m.Tp + tp, 0, 3000);
        }

        // ---- the claim ---------------------------------------------------------------------------------------------

        // CCharEntity::IsMobOwner
        private bool IsMobOwner(CharData c, MobEntity m)
        {
            if (m.OwnerId == 0 || m.OwnerId == c.Id || m.GetMobMod(MobMod.ClaimType) == ClaimNonExclusive) return true;
            foreach (var member in ForAlliance(c))
                if (member.Id == m.OwnerId) return true;
            return false;
        }

        private bool HasClaim(CharData c, MobEntity m)
        {
            if (m.OwnerId == c.Id) return true;
            foreach (var member in ForAlliance(c))
                if (member.Id == m.OwnerId) return true;
            return false;
        }

        // battleutils::ClaimMob: a char's pet or trust claims for its master, the enmity going to it (UpdateEnmity 0 / 0
        // with the master, still tameable: the first one on the list gets the initial 200 CE / 900 VE); a trust on top of
        // the list counts as its master
        private void ClaimMob(MobEntity m, BattleEntity original, long now)
        {
            if (m.Allegiance == CharData.AllegiancePlayer || m is PetEntity) return;
            if ((original as CharData ?? original.PMaster as CharData) is not CharData attacker) return;
            m.Enmity.UpdateEnmity(original, 0, 0, withMaster: true, tameable: true);
            if (m.GetMobMod(MobMod.ClaimType) == ClaimUnclaimable) return;
            var bt = original is MobEntity origMob ? origMob.BattleTarget : attacker.BattleTarget;
            bool alive = m.IsAlive && !m.IsDead;
            if (!(bt == null || bt == m || bt != attacker.ClaimedMob || !alive)) return;

            var old = attacker.ClaimedMob;
            if (alive && old != null && old != m && old.IsAlive && !old.IsDead && old.OwnerId == attacker.Id)
            {
                old.DeclaimTimeMs = now;
                attacker.ClaimedMob = null;
            }
            if (HasClaim(attacker, m))
            {
                m.OwnerId = attacker.Id;
                if (alive)
                {
                    m.UpdateMask |= EntityUpdatePacket.UpdateStatus;
                    attacker.ClaimedMob = m;
                }
                return;
            }
            if (!alive)
            {
                m.OwnerId = attacker.Id;
                return;
            }
            var highest = HighestEnmity(m);
            // a trust on top claims for its master
            if (highest is TrustEntity) highest = highest.PMaster;
            bool highestInAlliance = false;
            if (highest != null)
                foreach (var member in ForAlliance(attacker))
                    if (member == highest || member.PPet == highest) highestInAlliance = true;
            if (highest == null || highestInAlliance)
            {
                m.OwnerId = attacker.Id;
                m.UpdateMask |= EntityUpdatePacket.UpdateStatus;
                attacker.ClaimedMob = m;
            }
        }

        // CEnmityContainer::GetHighestEnmity over the chars still worth hating
        private BattleEntity HighestEnmity(MobEntity m) => MobHighestEnmity(m);

        // CPlayerController / battleutils::RelinquishClaim: a living mob the char owns passes to an alliance member in
        // the zone that is up and not holding another claim; otherwise its declaim timer starts
        private void RelinquishClaim(CharData c, long now)
        {
            var m = c.ClaimedMob;
            if (m == null) return;
            if (m.IsAlive && !m.IsDead && m.OwnerId == c.Id)
            {
                CharData passTo = null;
                foreach (var member in ForAlliance(c))
                    if (member != c && SameArea(member, c) && member.IsAlive && (member.ClaimedMob == null || member.ClaimedMob == m))
                    {
                        passTo = member;
                        break;
                    }
                if (passTo != null)
                {
                    m.OwnerId = passTo.Id;
                    m.UpdateMask |= EntityUpdatePacket.UpdateStatus;
                    passTo.ClaimedMob = m;
                }
                else m.DeclaimTimeMs = now;
            }
            c.ClaimedMob = null;
        }

        // ---- death -------------------------------------------------------------------------------------------------

        // CMobEntity::Die: the death state, the claimers let go, and the defeat message m_DropItemTime later (a
        // second, or a killing spell's animation time); the body despawns after 15 s, less DESPAWN_TIME_REDUCTION's
        // seconds (CDeathState::Update: the entry time + m_deathTime - the mod)
        private void MobDie(MobEntity m, long now)
        {
            if (m.PBattlefield != null) BattlefieldHandleDeath(m.PBattlefield, m);
            AbortMobSkill(m, now); // ClearStateStack
            AbortMagic(m, now);
            m.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Death, EffectNotice.Silent); // CDeathState
            m.Slot?.OnKill(m, now);
            m.DeathAtMs = now;
            m.DefeatMsgAtMs = now + m.DropItemTimeMs;
            m.DespawnAtMs = now + 15000 - m.GetMod(Mod.DESPAWN_TIME_REDUCTION) * 1000L;
            m.Animation = Anim.Death;
            m.UpdateMask |= EntityUpdatePacket.UpdateHp;
            m.Tp = 0;
            m.Enmity.Clear();
            m.Engaged = false;
            // an SMN's living pet dies with it (here at 0 HP, as a C# death needs)
            if (m.PPet is MobEntity pet && pet.IsAlive && !pet.IsDead && m.MJob == MobStats.JobSmn)
            {
                pet.Hp = 0;
                MobDie(pet, now);
            }
            foreach (var c in charSessions_.Keys)
                if (c.ClaimedMob == m) c.ClaimedMob = null;
            // CBattleEntity::Die: the DEATH listeners, with the owner as the killer
            var killer = m.OwnerId != 0 ? CharById(m.OwnerId).Item1 : null;
            if (killer != null) TriggerListener(m, "DEATH", m, killer);
            else TriggerListener(m, "DEATH", m);
            m.BattleTarget = null;
            // a charmed mob leaves its master
            if (m.PMaster is CharData charmMaster && charmMaster.PPet == m) DetachPet(charmMaster);
        }

        // the defeat message to the chars that see the mob, the kill's rewards, then the claim lifts
        private void MobDefeated(ZoneInfo info, MobEntity m, long now)
        {
            m.DefeatMsgAtMs = 0;
            // the last attacker: a char, a char's pet or trust
            BattleEntity killer = m.LastAttackerId != 0 ? CharById(m.LastAttackerId).Item1 : null;
            if (killer == null && m.LastAttackerId != 0) killer = info.Pets.Find(pet => pet.Id == m.LastAttackerId);
            if (killer == null && m.LastAttackerId != 0) killer = info.Trusts.Find(trust => trust.Id == m.LastAttackerId);
            if (killer == null && m.LastAttackerId != 0 && info.MobById.TryGetValue(m.LastAttackerId, out var mobKiller)) killer = mobKiller;
            var p = killer != null
                ? SelfPackets.BattleMessage(killer.Id, killer.TargId, m.Id, m.TargId, 0, 0, MsgDefeatsTarget)
                : SelfPackets.BattleMessage(m.Id, m.TargId, m.Id, m.TargId, 0, 0, MsgFallsToGround);
            foreach (var (c, s) in charSessions_)
                if (InArea(c, info) && c.SpawnMobList.Contains(m.Id)) s.PacketList.AddLast(new BasicPacket(p.Buffer));
            DistributeRewards(m, now);
            m.OwnerId = 0;
            m.ThLvl = m.GilfinderLevel = 0;
        }

        // the combat state SpawnMob clears
        private static void ResetCombat(MobEntity m)
        {
            m.LastAttackerId = 0;
            m.SkillState = null;
            m.MagicState = null;
            m.RangedState = null;
            m.InactiveState = null;
            m.DeathAtMs = m.DefeatMsgAtMs = m.DeclaimTimeMs = 0;
            m.DropItemTimeMs = 1000; // CMobEntity::Spawn
            m.Enmity.Clear();
            m.Engaged = false;
            m.BattleTarget = null;
            m.HiPcLvl = 0;
            m.HiPartySize = 0;
            m.GiveExp = true;
            m.LastHitAtkType = 0;
            m.ThLvl = m.GilfinderLevel = 0;
            m.NextAiTickMs = 0;
            m.EntityFlags &= ~MobEntity.EntityFlagCallForHelp;
        }

        // ---- call for help (0x01A Help) ----------------------------------------------------------------------------

        private void CallForHelp(MapSession s, CharData c)
        {
            if (c.BattleTarget is MobEntity m && !m.CallForHelp && m.Enmity.HasId(c.Id) && !m.CallForHelpBlocked)
            {
                // CMobEntity::SetCallForHelpFlag: the claim lifts
                m.EntityFlags |= MobEntity.EntityFlagCallForHelp;
                m.OwnerId = 0;
                m.UpdateMask |= EntityUpdatePacket.UpdateStatus;
                PushCharInRange(c, SelfPackets.BattleMessage(c.Id, c.TargId, c.Id, c.TargId, 0, 0, MsgCallForHelp), true);
                return;
            }
            Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgCannotCallForHelp));
        }

        // each tick: the chars' attack states, then the mobs' deaths, defeat messages, stale claims (CMobController's
        // declaim: an owner that has let go for 3 s loses it) and AI, then the chars' deaths
        private void TickCombat()
        {
            long now = NowMs();
            foreach (var (c, s) in new List<KeyValuePair<CharData, MapSession>>(charSessions_))
            {
                if (c.IsCharmed && c.PMaster != null) TickCharmedChar(s, c, now);
                else UpdateAttackState(s, c, now);
            }
            foreach (var (info, _, _) in AllAreas())
                // by index: a script may add a dynamic mob while this runs
                for (int i = 0; i < info.Mobs.Count; i++)
                {
                    var m = info.Mobs[i];
                    if (!m.IsSpawned) continue;
                    // a battlefield being cleaned up holds its mobs still
                    if (m.PBattlefield != null && m.PBattlefield.CanCleanup()) continue;
                    if (m.Hp == 0 && !m.IsDead) MobDie(m, now);
                    if (m.DefeatMsgAtMs != 0 && now >= m.DefeatMsgAtMs) MobDefeated(info, m, now);
                    if (m.OwnerId != 0 && !m.IsDead && m.DeclaimTimeMs != 0 && now >= m.DeclaimTimeMs + 3000 &&
                        CharById(m.OwnerId).Item1?.ClaimedMob != m)
                    {
                        m.OwnerId = 0;
                        m.DeclaimTimeMs = 0;
                        m.UpdateMask |= EntityUpdatePacket.UpdateStatus;
                    }
                    TickMobAi(m, now);
                }
            TickCharDeaths(now);
            foreach (var c in new List<CharData>(charSessions_.Keys)) FlushDirtyItems(c);
        }

        // InventorySyncState::flushDirtyItems (end of CCharEntity::PostTick): each item whose exdata a script changed
        // is saved (char_inventory.extra) and its ITEM_ATTR sent
        private void FlushDirtyItems(CharData c)
        {
            foreach (var item in c.Items)
            {
                if (!item.Dirty) continue;
                Exec("UPDATE char_inventory SET extra = ? WHERE charid = ? AND location = ? AND slot = ? LIMIT 1",
                     BoundValue.Of(item.Extra), BoundValue.Of(c.Id), BoundValue.Of((byte)item.Location), BoundValue.Of(item.Slot));
                PushTo(c, GameOkPackets.ItemAttr(item, GameOkPackets.LockFlagFor(c, item)));
                item.Dirty = false;
            }
        }
    }
}
