using System;
using System.Collections.Generic;
using XI.Script;

namespace XI.Zone
{
    // One swing of CBattleEntity::OnAttack's loop with the defender's reactions, shared by a char's swing at a mob and a
    // mob's at a char: Perfect Dodge, Defense Boost's deflection (CAttack::IsDeflected), the hit roll, a parry
    // (CAttack::CheckParried: xi.combat.physical.isParried, battleutils::HandleTacticalParry,
    // HandleIssekiganEnmityBonus), a shadow (battleutils::IsAbsorbByShadow: Utsusemi, Blink), Third Eye
    // (CAttack::CheckAnticipated: xi.combat.counter.checkSeiganCounter, xi.combat.physicalHitRate.checkAnticipated), a
    // counter (CAttack::CheckCounter, its damage through TakePhysicalDamage and the reaction block, or a shadow of the
    // attacker's taking it), the hit's crit, a guard (CAttack::CheckGuarded: xi.combat.physical.isGuarded,
    // HandleTacticalGuard), the damage, a shield block (xi.combat.physical.isBlocked, the block's absorption in
    // TakePhysicalDamage) and the defender's evasion skill-up.
    // A SATA round (Melee.Round.Sata: a sure right-hand hit under Sneak / Trick Attack) hits whatever the roll and goes
    // through shadows; the first swing's crit may be Sneak / Trick Attack's, and a Trick Attack hit's enmity goes to the
    // partner.
    // A hit, guard or block on a defender with HP left runs the additional effects (HandleEnspell,
    // ZoneHost.Enspell.cs), then the spikes or Retaliation (HandleSpikesDamage, ZoneHost.Spikes.cs); a parry under
    // Battuta its parry spikes. A covered swing lands on the cover user (CheckCover in the attack loops).
    // A hit under Feint puts Evasion Down on the target (Feint's sub power as its TREASURE_HUNTER_PROC) and ends Feint.
    // The swing's listeners see it as a CAttack (isCritical / setCritical): MELEE_SWING_HIT once the critical is rolled
    // (a listener may change it), MELEE_SWING_MISS on a miss, an anticipated or countered swing and a counter that
    // missed; a critical on a mob calls CRITICAL_TAKE and the mob's onCriticalHit.
    // An automaton skills up its melee on a hit and on a counter (puppetutils::TrySkillUP); a char's lost shadow marks
    // its effects for saving (setPersist).
    public sealed partial class ZoneHost
    {
        // xi::StatusEffect
        private const ushort EffectFeint = 343, EffectEvasionDown = 148;
        private const ushort EffectPerfectDodge = 49, EffectCounterstance = 61, EffectThirdEye = 67, EffectDefenseBoost = 93,
            EffectSeigan = 354, EffectPerfectCounter = 436, EffectCopyImage2 = 444, EffectCopyImage3 = 445, EffectIssekigan = 484;

        // msg_basic.h
        private const ushort MsgCounterAbsByShadow = 14, MsgTargetAnticipates = 30, MsgTargetDodges = 32,
            MsgAttackCounteredDamage = 33, MsgTargetParries = 70;

        // ActionResolution, ActionReactKind
        private const byte ResolutionHit = 0, ResolutionMiss = 1, ResolutionGuard = 2, ResolutionParry = 3, ResolutionBlock = 4;
        private const byte ReactCounter = 63;

        private const int TraitTacticalParry = 100, TraitTacticalGuard = 101;
        private const int MeritCounterRate = 454, MeritZanshinAttackRate = 1096; // xi::Merit
        private const int JpCounterstanceEffect = 0x045, JpIssekiganEffect = 0x1A8;

        private static bool DefenseIsEngaged(BattleEntity e) => e switch
        {
            MobEntity m => m.Engaged,
            CharData c => c.BattleTarget != null,
            _ => false,
        };

        // the swing at the result r, the animation already set
        private void MeleeSwing(BattleEntity attacker, BattleEntity defender, ActionResult r, Melee.Swing swing, long now)
        {
            var melee = MeleeFor();
            int slot = Melee.WeaponSlot(attacker, swing);
            bool countered = false, parried = false, critical = false;
            int processed = 0; // CAttack::GetDamage: what ProcessDamage made of the hit

            if (defender.StatusEffects.HasStatusEffectWithSubId(EffectPerfectDodge, 0))
            {
                r.MessageId = MsgTargetDodges;
                r.Resolution = ResolutionMiss;
            }
            else if (IsDeflected(attacker, defender))
            {
                r.MessageId = MsgHit;
                r.Resolution = ResolutionParry;
            }
            else if ((MobRng.Next(100) < melee.SwingHitRate(attacker, defender, swing) || swing.Round?.Sata == true) &&
                     !defender.StatusEffects.HasStatusEffect(EffectId.AllMiss))
            {
                // a Daken throw is never parried, anticipated or countered
                bool daken = swing.Type == Melee.AttackType.Daken;
                if (!daken && DefenseCallGlobalBool("xi.combat.physical.isParried", defender, attacker))
                {
                    parried = true;
                    r.MessageId = MsgTargetParries;
                    r.Resolution = ResolutionParry;
                    HandleTacticalParry(defender);
                    HandleIssekiganEnmityBonus(defender, attacker);
                }
                else if (swing.Round?.Sata != true && IsAbsorbByShadow(defender, attacker))
                {
                    r.MessageId = MsgShadowAbsorb;
                    r.Param = 1;
                    r.Resolution = ResolutionMiss;
                }
                else if (!daken && (CheckAnticipated(attacker, defender, ref countered, ref critical, out bool anticipated) ||
                                    CheckCounter(attacker, defender, ref countered, ref critical, swing)))
                {
                    if (anticipated)
                    {
                        r.MessageId = MsgTargetAnticipates;
                        r.Resolution = ResolutionMiss;
                    }
                    if (countered)
                    {
                        r.Resolution = ResolutionMiss;
                        r.SpikesEffect = ReactCounter;
                        if (IsAbsorbByShadow(attacker, defender))
                        {
                            r.SpikesParam = 1;
                            r.SpikesMessage = MsgCounterAbsByShadow;
                            r.MessageId = 0;
                            r.Param = 0;
                        }
                        else CounterDamage(attacker, defender, r, critical, now);
                    }
                    TriggerListener(attacker, "MELEE_SWING_MISS", attacker, defender, swing);
                }
                else
                {
                    critical = MobRng.Next(100) < melee.CritHitRate(attacker, defender, slot, !swing.First);
                    // the listeners may make or unmake the critical (CLuaAttack::setCritical)
                    swing.Critical = critical;
                    TriggerListener(attacker, "MELEE_SWING_HIT", attacker, defender, swing);
                    critical = swing.Critical;
                    r.Resolution = ResolutionHit;
                    if (critical) r.Info |= 2; // ActionInfo::CriticalHit
                    r.MessageId = critical ? (daken ? MsgRangedCrit : MsgCrit) : (daken ? MsgRangedHit : MsgHit);
                    if (critical && defender is MobEntity critMob)
                    {
                        TriggerListener(critMob, "CRITICAL_TAKE", critMob, attacker);
                        CallMobHook(critMob, "onCriticalHit", critMob, attacker);
                    }
                    float ratio = melee.SwingDamageRatio(attacker, defender, swing, critical);

                    // CAttack::CheckGuarded
                    if (DefenseCallGlobalBool("xi.combat.physical.isGuarded", defender, attacker))
                    {
                        ratio = Math.Max(ratio - 1.0f, 0f);
                        r.Resolution = ResolutionGuard;
                        HandleTacticalGuard(defender);
                    }

                    // Feint: the target's evasion down (3 s ticks, 30 s), Feint's sub power riding on it as a
                    // TREASURE_HUNTER_PROC, then Feint goes
                    if (attacker.StatusEffects.GetStatusEffect(EffectFeint) is { } feint)
                    {
                        if (defender.StatusEffects.AddStatusEffect(new StatusEffect(EffectEvasionDown, EffectEvasionDown, feint.Power, 3000, 30_000)))
                            defender.StatusEffects.GetStatusEffect(EffectEvasionDown)?.AddMod(Mod.TREASURE_HUNTER_PROC, (short)feint.SubPower);
                        attacker.StatusEffects.DelStatusEffect(EffectFeint);
                    }

                    int dmg = melee.ProcessDamage(attacker, defender, swing, ratio);
                    processed = dmg;
                    // ProcessDamage's skill-up: a char's weapon skill (throwing for a Daken throw), an automaton's melee
                    if (dmg > 0)
                    {
                        if (attacker is CharData sc)
                        {
                            if (daken) TrySkillUp(sc, Skill.Throwing, defender.MLvl, false, false);
                            else if (CharStats.WeaponIn(sc, slot) is { } weapon) TrySkillUp(sc, weapon.Skill, defender.MLvl, false, false);
                        }
                        else if (attacker is AutomatonEntity automaton && automaton.PMaster is CharData)
                            AutomatonTrySkillUp(automaton, SkillAutomatonMelee, defender.MLvl);
                    }
                    bool blocked = DefenseCallGlobalBool("xi.combat.physical.isBlocked", defender, attacker);
                    if (blocked) r.Resolution = ResolutionBlock;

                    dmg = SwingTakePhysicalDamage(attacker, defender, dmg, slot, blocked, true, false, now, swing.Round?.TaEntity,
                                                  swing.Covered, swing.Round?.OriginalTarget, swing.Type == Melee.AttackType.Zanshin);
                    if (dmg < 0)
                    {
                        r.Param = -dmg;
                        r.MessageId = MsgAbsorb;
                    }
                    else
                    {
                        // action_result_t::recordDamage
                        r.Param = dmg;
                        if (defender.Hp <= 0) r.Info |= 1;
                        int pct = defender.MaxHp > 0 ? dmg * 100 / defender.MaxHp : 0;
                        r.HitDistortion = (byte)(pct >= 20 ? 3 : pct >= 10 ? 2 : pct > 0 ? 1 : 0);
                    }
                }

                if (defender is CharData pc && !countered && !parried) TrySkillUp(pc, Skill.Evasion, attacker.MLvl, false, false);
            }
            else
            {
                r.Resolution = ResolutionMiss;
                r.MessageId = MsgMiss;
                // the evasion skill-up on an evaded swing too
                if (defender is CharData pc) TrySkillUp(pc, Skill.Evasion, attacker.MLvl, false, false);
                TriggerListener(attacker, "MELEE_SWING_MISS", attacker, defender, swing);
            }

            // no hit at all: no damage unless a shadow went
            if (r.Resolution == ResolutionMiss && r.MessageId != MsgShadowAbsorb) r.Param = 0;

            // the additional effects for a hit, guard or block (not a Daken throw), while the target has HP, then the
            // spikes
            if (r.Resolution != ResolutionMiss && r.Resolution != ResolutionParry && swing.Type != Melee.AttackType.Daken)
            {
                if (defender.Hp > 0) HandleEnspell(attacker, defender, r, swing, slot, processed, now);
                HandleSpikesDamage(attacker, defender, r, processed, now);
            }
            // a parry under Battuta
            if (r.Resolution == ResolutionParry && defender.StatusEffects.HasStatusEffect(EffectBattuta))
                HandleParrySpikesDamage(attacker, defender, r, processed, now);
            lua_?.Forget(swing);
        }

        // the CAttack Lua type (CLuaAttack) over a swing
        private const string AttackLuaType = "CAttack";

        private void RegisterAttackType()
        {
            static Melee.Swing A(LuaState l) => l.ToObject(1) as Melee.Swing ?? throw new InvalidOperationException("bad self: expected " + AttackLuaType);
            lua_.RegisterType(AttackLuaType, new Dictionary<string, LuaFunction>
            {
                ["isCritical"] = l =>
                {
                    l.Push(A(l).Critical);
                    return 1;
                },
                ["setCritical"] = l =>
                {
                    A(l).Critical = l.ToBoolean(2);
                    return 0;
                },
            });
        }

        private const ushort EffectHasso = 353;

        // CBattleEntity::OnAttack's Zanshin: only on a single-swing round, last in the multi-hit order; ZANSHIN (and a
        // char's merits) for a missed, guarded or countered first swing, or a quarter of it under a SAM's Hasso
        private void TryZanshin(BattleEntity attacker, Melee.Swing swing, ActionResult r, System.Collections.Generic.List<Melee.Swing> swings)
        {
            if (!swing.First || swings.Count != 1) return;
            int chance = attacker.GetMod(Mod.ZANSHIN) + (attacker is CharData c ? c.Merits?.Value(c, MeritZanshinAttackRate) ?? 0 : 0);
            chance = Math.Clamp(chance, 0, 100);
            bool missedOrCountered = r.Resolution != ResolutionHit || r.SpikesEffect == ReactCounter;
            bool normalProc = missedOrCountered && MobRng.Next(100) < chance;
            bool samWithHasso = attacker.GetMod(Mod.HASSO_ZANSHIN_BONUS) > 0 && attacker.StatusEffects.HasStatusEffect(EffectHasso);
            bool hassoProc = samWithHasso && MobRng.Next(100) < chance / 4;
            if (normalProc || hassoProc)
                swings.Add(new Melee.Swing { Type = Melee.AttackType.Zanshin, Direction = Melee.Direction.Right, Round = swing.Round });
        }

        // battleutils::TakePhysicalDamage, either way round
        private int SwingTakePhysicalDamage(BattleEntity attacker, BattleEntity defender, int damage, int slot, bool isBlocked,
            bool giveTpToAttacker, bool isCounter, long now, BattleEntity taChar = null, bool isCovered = false,
            BattleEntity originalTarget = null, bool isZanshin = false) => (attacker, defender) switch
        {
            (CharData c, MobEntity m) => TakePhysicalDamage(c, m, damage, slot, now, isBlocked, giveTpToAttacker, isCounter, taChar, isZanshin),
            (MobEntity m, CharData c) => CharTakePhysicalDamage(m, c, damage, slot, isBlocked, giveTpToAttacker, isCounter, isCovered, originalTarget, isZanshin),
            // a charmed char and a char of its former side, either way round
            (CharData a, CharData d) => CharTakePhysicalDamage(a, d, damage, slot, isBlocked, giveTpToAttacker, isCounter, isCovered, originalTarget, isZanshin),
            (PetEntity pet, MobEntity m) => MobTakePhysicalDamageFromPet(pet, m, damage, slot, now, isBlocked, giveTpToAttacker, isCounter),
            (MobEntity m, PetEntity pet) => PetTakePhysicalDamage(m, pet, damage, slot, now, isBlocked, giveTpToAttacker, isCounter),
            // a char's trust: hit and hitting as a pet
            (TrustEntity trust, MobEntity m) => MobTakePhysicalDamageFromPet(trust, m, damage, slot, now, isBlocked, giveTpToAttacker, isCounter),
            (MobEntity m, TrustEntity trust) => PetTakePhysicalDamage(m, trust, damage, slot, now, isBlocked, giveTpToAttacker, isCounter),
            // a charmed mob either way round
            (MobEntity charmed, MobEntity m) when IsCharmedPet(charmed) =>
                MobTakePhysicalDamageFromPet(charmed, m, damage, slot, now, isBlocked, giveTpToAttacker, isCounter),
            (MobEntity m, MobEntity charmed) when IsCharmedPet(charmed) =>
                PetTakePhysicalDamage(m, charmed, damage, slot, now, isBlocked, giveTpToAttacker, isCounter),
            _ => 0,
        };

        // CAttack::IsDeflected: Defense Boost's subpower is the cone the attacker must stand in
        private static bool IsDeflected(BattleEntity attacker, BattleEntity defender)
        {
            var boost = defender.StatusEffects.GetStatusEffect(EffectDefenseBoost);
            if (boost == null || boost.SubPower == 0) return false;
            // infront(attacker, victim, subpower)
            return Math.Abs(FacingAngle(PosOf(defender), PosOf(attacker))) < (byte)boost.SubPower / 2;
        }

        // battleutils::HandleTacticalParry
        private void HandleTacticalParry(BattleEntity e)
        {
            if (e is CharData c && CharStats.HasTrait(c, TraitTacticalParry)) AddTp(c, c.GetMod(Mod.TACTICAL_PARRY));
        }

        // battleutils::HandleTacticalGuard
        private void HandleTacticalGuard(BattleEntity e)
        {
            if (e is CharData c && CharStats.HasTrait(c, TraitTacticalGuard)) AddTp(c, c.GetMod(Mod.TACTICAL_GUARD));
        }

        // battleutils::HandleIssekiganEnmityBonus: 300 CE a parry, job points 10 VE each
        private static void HandleIssekiganEnmityBonus(BattleEntity defender, BattleEntity attacker)
        {
            if (attacker is MobEntity m && defender.StatusEffects.HasStatusEffect(EffectIssekigan))
            {
                int jpBonus = defender is CharData c ? JobPointUtil.Value(c, JpIssekiganEffect) * 10 : 0;
                m.Enmity.UpdateEnmity(defender, 300, jpBonus);
            }
        }

        // battleutils::IsAbsorbByShadow: Utsusemi first (it always overwrites Blink), else Blink at 80%; a char's
        // Utsusemi shadow costs the mob's 25 CE toward it, the last one takes the effect off and fewer than 4 change
        // Copy Image's icon
        private bool IsAbsorbByShadow(BattleEntity defender, BattleEntity attacker)
        {
            int shadow = defender.GetMod(Mod.UTSUSEMI);
            var modShadow = Mod.UTSUSEMI;
            if (shadow == 0)
            {
                shadow = defender.GetMod(Mod.BLINK);
                modShadow = Mod.BLINK;
                if (MobRng.Next(100) < 20) return false;
            }
            if (shadow <= 0) return false;

            defender.SetModifier(modShadow, (short)--shadow);
            if (defender is CharData shadowed) shadowed.Persist |= CharPersist.Effects;
            if (modShadow == Mod.UTSUSEMI && defender is CharData && attacker is MobEntity mob) mob.Enmity.UpdateEnmity(defender, -25, 0);
            if (shadow == 0)
            {
                defender.StatusEffects.DelStatusEffect(modShadow == Mod.UTSUSEMI ? EffectId.CopyImage : EffectId.Blink);
            }
            else if (shadow < 4 && modShadow == Mod.UTSUSEMI && defender is CharData)
            {
                var copyImage = defender.StatusEffects.GetStatusEffect(EffectId.CopyImage);
                copyImage?.SetIcon(shadow switch { 1 => EffectId.CopyImage, 2 => EffectCopyImage2, _ => EffectCopyImage3 });
            }
            return true;
        }

        // CAttack::CheckAnticipated: with Third Eye up, Seigan's counter then the anticipation itself; true whenever the
        // script answered
        private bool CheckAnticipated(BattleEntity attacker, BattleEntity defender, ref bool countered, ref bool critical, out bool anticipated)
        {
            anticipated = false;
            if (defender.StatusEffects.GetStatusEffect(EffectThirdEye, 0) == null) return false;

            if (DefenseCallGlobal("xi.combat.counter.checkSeiganCounter", attacker, defender) is bool seigan)
            {
                countered = seigan;
                if (countered) critical = MobRng.Next(100) < MeleeFor().CritHitRate(defender, attacker, CharStats.SlotMain);
            }
            if (DefenseCallGlobal("xi.combat.physicalHitRate.checkAnticipated", attacker, defender) is bool a)
            {
                anticipated = a;
                return true;
            }
            return false;
        }

        // CAttack::CheckCounter: an engaged defender able to act, facing an attacker without Perfect Dodge; COUNTER (and a
        // MNK's merits) to 80%, or a quarter of Seigan's zanshin rate, then the defender's own hit roll; else Perfect
        // Counter, always critical, goes
        private bool CheckCounter(BattleEntity attacker, BattleEntity defender, ref bool countered, ref bool critical, Melee.Swing swing = null)
        {
            if (!DefenseIsEngaged(defender) || defender.StatusEffects.HasPreventActionEffect(true))
            {
                countered = false;
                return countered;
            }

            int meritCounter = 0;
            if (defender is CharData mnk && mnk.MJob == MobStats.JobMnk) meritCounter = mnk.Merits?.Value(mnk, MeritCounterRate) ?? 0;

            int seiganChance = 0;
            if (defender is CharData sam && sam.GetMod(Mod.SEIGAN_COUNTER_BONUS) > 0)
            {
                bool twoHanded = sam.MainWeapon is { TwoHanded: true };
                if (twoHanded && sam.StatusEffects.HasStatusEffectWithSubId(EffectSeigan, 0))
                {
                    seiganChance = sam.GetMod(Mod.ZANSHIN) + (sam.Merits?.Value(sam, MeritZanshinAttackRate) ?? 0);
                    seiganChance = (ushort)Math.Clamp((ushort)seiganChance, (ushort)0, (ushort)100);
                    seiganChance /= 4;
                }
            }

            if (!attacker.StatusEffects.HasStatusEffect(EffectPerfectDodge) && Facing(defender, attacker))
            {
                var melee = MeleeFor();
                if (MobRng.Next(100) < Math.Clamp(defender.GetMod(Mod.COUNTER) + meritCounter, 0, 80) || MobRng.Next(100) < seiganChance)
                {
                    if (MobRng.Next(100) < melee.HitRate(defender, attacker, 0))
                    {
                        countered = true;
                        critical = MobRng.Next(100) < melee.CritHitRate(defender, attacker, CharStats.SlotMain);
                    }
                    else if (swing != null) TriggerListener(attacker, "MELEE_SWING_MISS", attacker, defender, swing);
                }
                else if (defender.StatusEffects.HasStatusEffect(EffectPerfectCounter))
                {
                    countered = true;
                    critical = true;
                    defender.StatusEffects.DelStatusEffect(EffectPerfectCounter);
                }
            }
            return countered;
        }

        // OnAttack's counter: the defender's main weapon (a char's fists with their natural damage, a mob's with the
        // region's hand-to-hand penalty) against the attacker, Counterstance's job point attack bonus, COUNTER_DAMAGE;
        // the attacker takes it with no TP to the defender
        private void CounterDamage(BattleEntity attacker, BattleEntity defender, ActionResult r, bool critical, long now)
        {
            var melee = MeleeFor();
            int naturalH2h = 0;
            byte skillType = defender is CharData dc ? dc.MainWeapon?.Skill ?? (byte)Skill.HandToHand : (byte)Skill.None;
            float mobH2hPenalty = 1.0f;
            if (defender is CharData && skillType == Skill.HandToHand)
                naturalH2h = (int)MathF.Floor(Melee.GetSkill(defender, Skill.HandToHand) * 0.11f + 3);
            else if (defender is MobEntity dm && Melee.MainSkill(dm) == Skill.HandToHand && dm.GetMobMod(MobMod.NoH2hPenalty) == 0)
                mobH2hPenalty = ZoneRegions.Of(dm.Zone) <= 27 ? 0.425f : 0.650f; // REGION_TYPE::LIMBUS: pre-ToAU

            float attBonus = 1.0f;
            if (defender is CharData cs && cs.MJob == MobStats.JobMnk && cs.StatusEffects.HasStatusEffect(EffectCounterstance))
            {
                float csJpModifier = JobPointUtil.Value(cs, JpCounterstanceEffect) * 2;
                float targetDex = Melee.Dex(cs);
                attBonus += Math.Max(targetDex / 100f * csJpModifier, 0f);
            }

            float ratio = melee.DamageRatio(defender, attacker, critical, skillType, CharStats.SlotMain, attBonus);
            int extraCounterDmg = defender.GetMod(Mod.COUNTER_DAMAGE);
            int damage = Math.Max(Melee.MainWeaponDmg(defender) + naturalH2h + extraCounterDmg + Melee.FStr(defender, attacker), 0);
            damage = (int)MathF.Floor(damage * mobH2hPenalty * ratio);

            r.SpikesParam = SwingTakePhysicalDamage(defender, attacker, damage, CharStats.SlotMain, false, false, true, now);
            r.SpikesMessage = MsgAttackCounteredDamage;
            if (defender is CharData skiller) TrySkillUp(skiller, skillType, attacker.MLvl, false, false);
            // In case the Automaton can counter
            else if (defender is AutomatonEntity automaton && automaton.PMaster is CharData)
                AutomatonTrySkillUp(automaton, SkillAutomatonMelee, attacker.MLvl);
        }

        // luautils::callGlobal<bool>: false when the function is missing or fails
        private bool DefenseCallGlobalBool(string path, params object[] args) => DefenseCallGlobal(path, args) ?? false;

        // a global's first result as a bool, null when it is missing or fails (sol's !result.valid())
        private bool? DefenseCallGlobal(string path, params object[] args)
        {
            if (lua_ == null) return null;
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (lua_.IsNil(-1))
                {
                    log_($"luautils::callGlobalFunction: {path}: Function not found");
                    return null;
                }
                foreach (var a in args) lua_.Push(a);
                lua_.Call(args.Length, 1);
                return lua_.ToBoolean(-1);
            }
            catch (LuaException e)
            {
                log_($"luautils::callGlobalFunction: {path}: {e.Message}");
                return null;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }
    }
}
