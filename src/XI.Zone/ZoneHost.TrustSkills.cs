using System;
using System.Collections.Generic;
using System.Linq;
using XI.Map;

namespace XI.Zone
{
    // A trust's TP skills: LoadTrustStatsAndSkills' tail (its mob skill list split into weapon skills, ids up to 255,
    // and mob skills, the level 3 skillchains only from level 60), CGambitsContainer::TryTrustSkill (the TP trigger
    // setTrustTPSkillSettings picks: at once, at random above a TP, as an opener once a party member holds TP, as a
    // closer; then the skill: random or the best closer, the last one as an opener, Ayame's closer for its master's
    // last weapon skill, August's Daybreak rotation) and a trust's CWeaponSkillState (CTrustEntity::OnWeaponSkillFinished:
    // the skill's script against the target, or its area, the skillchain on the primary target).
    public sealed partial class ZoneHost
    {
        // G_TP_TRIGGER
        private const ushort GTpTriggerAsap = 0, GTpTriggerRandom = 1, GTpTriggerOpener = 2, GTpTriggerCloser = 3,
            GTpTriggerCloserUntilTp = 4;
        private const ushort GSelectSpecialAyame = 5, GSelectSpecialAugust = 6;
        private const uint AugustNoQuarter = 3658;
        private static readonly uint[] AugustDaybreakWs = { 3656, 3657 };
        private static readonly uint[] AugustRegularWs = { 3653, 3654, 3655 };
        private const string GambitLastDaybreakVar = "[Gambit]LastDaybreakSkill";

        private WeaponSkill WeaponSkillById(int id) => (loader_.Stats as CharStats)?.WeaponSkills.Get((ushort)id);

        // trustutils::LoadTrustStatsAndSkills' skills: the default TP selectors, then the list. A missing skill ends the
        // list; a mob skill goes in twice when allowed (once as read, once after the level check), as in the C++.
        private void LoadTrustTpSkills(TrustEntity trust)
        {
            trust.TpTrigger = GTpTriggerAsap;
            trust.TpSelect = GSelectRandom;
            trust.TpSkills.Clear();
            foreach (ushort skillId in GetMobSkillList(trust.MobSkillList))
            {
                TrustSkill skill;
                if (skillId <= 255) // Player WSs
                {
                    var ws = WeaponSkillById(skillId);
                    if (ws == null)
                    {
                        log_($"LoadTrustStatsAndSkills: Error loading WeaponSkill id {skillId} for trust {trust.Name}");
                        break;
                    }
                    skill = new TrustSkill
                    {
                        Reaction = GReactionWs, SkillId = skillId, Primary = ws.PrimarySc, Secondary = ws.SecondarySc,
                        Tertiary = ws.TertiarySc, ValidTargets = IsSelfTargetWeaponSkill(skillId) ? TargetSelf : TargetEnemy,
                    };
                }
                else // MobSkills
                {
                    var ms = GetMobSkill(skillId);
                    if (ms == null)
                    {
                        log_($"LoadTrustStatsAndSkills: Error loading MobSkill id {skillId} for trust {trust.Name}");
                        break;
                    }
                    skill = new TrustSkill
                    {
                        Reaction = GReactionMs, SkillId = skillId, Primary = ms.PrimarySc, Secondary = ms.SecondarySc,
                        Tertiary = ms.TertiarySc, ValidTargets = ms.ValidTargets,
                    };
                    trust.TpSkills.Add(skill);
                }

                // Only get access to skills that produce Lv3 SCs after Lv60
                bool canFormLv3Skillchain = skill.Primary >= ScGravitation || skill.Secondary >= ScGravitation || skill.Tertiary >= ScGravitation;
                // Special case for Zeid II and others who only have Lv3+ skills
                bool onlyHasLv3Skillchains = canFormLv3Skillchain && trust.TpSkills.Count == 0;
                if (!canFormLv3Skillchain || trust.MLvl >= 60 || onlyHasLv3Skillchains) trust.TpSkills.Add(skill);
            }
        }

        private static List<byte> ResonanceOf(StatusEffect sc)
        {
            var resonance = new List<byte>();
            if (sc.Power != 0)
            {
                resonance.Add((byte)(sc.Power & 0xF));
                resonance.Add((byte)(sc.Power >> 4 & 0xF));
                resonance.Add((byte)(sc.Power >> 8));
            }
            return resonance;
        }

        // CGambitsContainer::TryTrustSkill
        private bool TryTrustSkill(TrustEntity trust, long now)
        {
            var target = trust.BattleTarget;
            if (target == null) return false;

            bool CheckTpTrigger()
            {
                if (trust.Tp >= 3000) return true; // Go, go, go!
                switch (trust.TpTrigger)
                {
                    case GTpTriggerAsap:
                        return true;
                    case GTpTriggerRandom:
                        // If the value provided by the script is missing or too low: the minimum TP Hold Threshold
                        if (trust.TpValue <= 1000) trust.TpValue = 1000;
                        return trust.Tp >= trust.TpValue && MobRng.Next(10000) < trust.TpValue;
                    case GTpTriggerOpener:
                    {
                        bool result = false;
                        if (trust.PMaster is CharData master)
                            foreach (var member in PartyWithTrusts(master))
                            {
                                if (trust.TpValue <= 1000) trust.TpValue = 1000;
                                if (member.Tp >= trust.TpValue && member != trust) result = true;
                            }
                        return result;
                    }
                    case GTpTriggerCloser: // Hold TP indefinitely to close a SC.
                    {
                        var sc = target.StatusEffects.GetStatusEffect(EffectSkillchain);
                        return sc != null && sc.StartMs + 3000 < now && sc.Tier == 0;
                    }
                    case GTpTriggerCloserUntilTp: // Will hold TP to close a SC, but WS immediately once specified value is reached.
                    {
                        if (trust.TpValue <= 1500) trust.TpValue = 1500;
                        if (trust.Tp >= trust.TpValue) return true; // Time to WS!
                        var sc = target.StatusEffects.GetStatusEffect(EffectSkillchain);
                        return sc != null && sc.StartMs + 3000 < now && sc.Tier == 0;
                    }
                    default:
                        return false;
                }
            }

            // the closer with the best skillchain against these resonance properties (the later one on a tie)
            void BestCloser(List<byte> resonance, ref TrustSkill chosen, ref byte chosenSkillchain)
            {
                foreach (var skill in trust.TpSkills)
                {
                    byte possible = FormSkillchain(resonance, new List<byte> { skill.Primary, skill.Secondary, skill.Tertiary });
                    if (possible != ScNone && possible >= chosenSkillchain)
                    {
                        chosen = skill;
                        chosenSkillchain = possible;
                    }
                }
            }

            TrustSkill chosenSkill = null;
            byte chosenSkillchain = ScNone;
            if (CheckTpTrigger() && trust.TpSkills.Count > 0)
            {
                switch (trust.TpSelect)
                {
                    case GSelectRandom:
                    {
                        var sc = target.StatusEffects.GetStatusEffect(EffectSkillchain);
                        // Opener, if no skillchain available select a random ws
                        if (sc == null) chosenSkill = trust.TpSkills[MobRng.Next(trust.TpSkills.Count)];
                        // Closer, the one that creates the best skillchain
                        else BestCloser(ResonanceOf(sc), ref chosenSkill, ref chosenSkillchain);
                        break;
                    }
                    case GSelectHighest: // Form the best possible skillchain
                    {
                        var sc = target.StatusEffects.GetStatusEffect(EffectSkillchain);
                        if (sc == null) chosenSkill = trust.TpSkills[^1]; // Opener: relies on the skills' order
                        else BestCloser(ResonanceOf(sc), ref chosenSkill, ref chosenSkillchain);
                        break;
                    }
                    case GSelectSpecialAyame:
                    {
                        var last = (trust.PMaster as CharData)?.LastWeaponSkill;
                        if (last != null)
                        {
                            // the master's weapon skill closing on each of hers
                            foreach (var skill in trust.TpSkills)
                            {
                                byte possible = FormSkillchain(new List<byte> { skill.Primary, skill.Secondary, skill.Tertiary },
                                    new List<byte> { last.PrimarySc, last.SecondarySc, last.TertiarySc });
                                if (possible != ScNone && possible >= chosenSkillchain)
                                {
                                    chosenSkill = skill;
                                    chosenSkillchain = possible;
                                }
                            }
                        }
                        else chosenSkill = trust.TpSkills[^1];
                        break;
                    }
                    case GSelectSpecialAugust:
                    {
                        // Daybreak active is sub animation 5, retail does the same thing.
                        bool maybeDaybreakActive = trust.AnimationSub == 5;
                        uint lastSkillUsed = trust.LocalVars.TryGetValue(GambitLastDaybreakVar, out var v) ? v : 0;
                        var candidates = new List<TrustSkill>();
                        if (maybeDaybreakActive)
                        {
                            // Only trigger No Quarter if the last skill used was ACTUALLY a Daybreak opener
                            if (AugustDaybreakWs.Contains(lastSkillUsed))
                                chosenSkill = trust.TpSkills.FirstOrDefault(s => s.SkillId == AugustNoQuarter);
                            if (chosenSkill == null) candidates.AddRange(trust.TpSkills.Where(s => AugustDaybreakWs.Contains(s.SkillId)));
                        }
                        else
                        {
                            // Normal state: use standard rotation
                            candidates.AddRange(trust.TpSkills.Where(s => AugustRegularWs.Contains(s.SkillId)));
                            // Clear the localVar so the next Daybreak starts fresh
                            if (lastSkillUsed != 0) trust.LocalVars[GambitLastDaybreakVar] = 0;
                        }
                        if (candidates.Count > 0 && chosenSkill == null) chosenSkill = candidates[MobRng.Next(candidates.Count)];
                        // Only update the localVar if actually picked a skill
                        if (chosenSkill != null) trust.LocalVars[GambitLastDaybreakVar] = chosenSkill.SkillId;
                        break;
                    }
                }
            }

            if (chosenSkill == null) return false;
            if (chosenSkill.Reaction == GReactionWs)
            {
                var ws = WeaponSkillById(chosenSkill.SkillId);
                if (ws == null)
                {
                    log_("G_REACTION::WS: PWeaponSkill was null.");
                    return false;
                }
                var wsTarget = (chosenSkill.ValidTargets & TargetSelf) != 0 ? trust : trust.BattleTarget;
                TrustWeaponSkill(trust, wsTarget, ws.Id, now);
            }
            else // Mobskill
            {
                var msTarget = (chosenSkill.ValidTargets & (TargetSelf | TargetPlayerParty)) != 0 ? trust : trust.BattleTarget;
                MobUseSkill(trust, msTarget, chosenSkill.SkillId, null, now);
            }
            return true;
        }

        // ---- a trust's weapon skill --------------------------------------------------------------------------------

        // CController::WeaponSkill -> CAIContainer::Internal_WeaponSkill -> CWeaponSkillState::init for a trust: not on
        // an untargetable target, only when the state can change; the skill, a valid target (itself for a self-target
        // one, else an enemy), then the "readies" BATTLE2 to the chars in range
        private bool TrustWeaponSkill(TrustEntity trust, BattleEntity target, ushort wsid, long now)
        {
            if (target == null || IsUntargetable(target) || !CanChangeState(trust)) return false;
            var ws = WeaponSkillById(wsid);
            if (ws == null) return false;
            bool valid = IsSelfTargetWeaponSkill(wsid) ? target == trust : ValidSkillTarget(target, trust, TargetEnemy);
            if (!valid) return false;
            if (!CanSeeTarget(trust, target)) return false;
            trust.WsState = new WeaponSkillState { Skill = ws, Target = target, EntryMs = now };
            var action = new ActionPacket { ActorId = trust.Id, ActionType = ActionCategory.SkillStart, ActionId = (uint)FourCC.SkillUse };
            var r = action.AddTarget(target.Id).AddResult();
            r.Param = ws.Id;
            r.MessageId = MsgWsReadies;
            PushMobInRange(trust, Battle2Packets.Battle2(action));
            return true;
        }

        // CWeaponSkillState::Update for a trust: true once the state is done
        private bool UpdateTrustWeaponSkill(TrustEntity trust, WeaponSkillState st, long now)
        {
            if (trust.IsAlive && !trust.IsDead && !st.Completed)
            {
                var target = st.Target;
                var action = new ActionPacket();
                if (target != null && EntityExists(target) && target.IsAlive)
                {
                    SpendWeaponSkillCost(trust, st);
                    OnTrustWeaponSkillFinished(trust, st, action);
                    // Only send packet if action was populated (e.g. interrupts return early)
                    if (action.Targets.Count > 0) PushMobInRange(trust, Battle2Packets.Battle2(action));

                    if (trust.StatusEffects.GetStatusEffect(WsEffectRestraint) is { } restraint)
                    {
                        ushort wsBonus = restraint.Power;
                        restraint.Power = 0;
                        restraint.SubPower = 0;
                        trust.DelModifier(Mod.ALL_WSDMG_FIRST_HIT, (short)wsBonus);
                    }

                    // the category changes upon being out of range
                    if (action.ActionType == ActionCategory.SkillFinish)
                    {
                        uint weaponskillVar = LocalVars(target).TryGetValue("weaponskillHit", out var v) ? v : 0;
                        TriggerListener(trust, "WEAPONSKILL_USE", trust, target, st.Skill, st.Spent, action, (int)(weaponskillVar & 0xFFFFFF));
                        foreach (var t in action.Targets)
                            if (TrustSkillTargetById(trust, t.ActorId) is { } pt)
                                TriggerListener(pt, "WEAPONSKILL_TAKE", trust, pt, st.Skill, st.Spent, action);
                    }
                }
                st.FinishMs = now + st.Skill.AnimationTimeMs;
                st.Completed = true;
            }
            else if (now > st.FinishMs)
            {
                return true;
            }
            return false;
        }

        // the ids a trust's action may name: a mob, a char, a char's pet, a trust
        private BattleEntity TrustSkillTargetById(MobEntity trust, uint id) =>
            WsBattleById(id) ?? AreaOf(trust).Trusts.Find(t => t.Id == id);

        // CWeaponSkillState::Cleanup for a trust; the suspended attack state resumes where its swing timer stood
        private void EndTrustWeaponSkill(TrustEntity trust, WeaponSkillState st, long now)
        {
            trust.WsState = null;
            if (trust.Engaged) trust.SwingAtMs += now - st.EntryMs;
            if (trust.IsAlive && !trust.IsDead) TriggerListener(trust, "WEAPONSKILL_STATE_EXIT", trust, (int)st.Skill.Id, st.Completed);
        }

        // CTrustEntity::OnWeaponSkillFinished: CBattleEntity's category and id; in range, the skill's script on each
        // target (the area around the target for an AoE), onWeaponskillHit on a mob, a self-target one's MP, the
        // skillchain on the primary target; out of range, the interrupt; then processActionEffectFlags
        private void OnTrustWeaponSkillFinished(TrustEntity trust, WeaponSkillState st, ActionPacket action)
        {
            var ws = st.Skill;
            action.ActorId = trust.Id;
            action.ActionType = ActionCategory.SkillFinish;
            action.ActionId = ws.Id;

            var battleTarget = st.Target;
            if (battleTarget == null) return;
            // battleutils::CalculateWeaponSkillTP: the TP bonus, capped (no weapons to take it off)
            short tp = (short)Math.Min(st.Spent + trust.GetMod(Mod.TP_BONUS), 3000);

            if (Distance(trust, battleTarget) <= ws.Range + HitboxSize(battleTarget) + HitboxSize(trust))
            {
                var targets = WsTargetFind(trust, battleTarget, ws);
                // There used to be an assumed interrupt handler
                if (targets.Count == 0) return;

                foreach (var t in targets)
                {
                    bool primary = t == battleTarget;
                    var r = action.AddTarget(t.Id).AddResult();
                    r.Resolution = 0; // ActionResolution::Hit
                    r.Animation = ws.AnimationId;
                    int damage = OnUseWeaponSkill(trust, t, ws, tp, primary, action);

                    if (!IsSelfTargetWeaponSkill(ws.Id))
                    {
                        if (primary && battleTarget is MobEntity bm and not TrustEntity and not PetEntity)
                            CallMobHook(bm, "onWeaponskillHit", bm, trust, (int)ws.Id);
                    }
                    else // Self-targetting WS restoring MP
                    {
                        r.MessageId = primary ? MsgWsUsesSkillRecoversMp : MsgWsTargetRecoversMp;
                        r.Resolution = 0;
                        damage = Math.Max(damage, 0);
                        r.Param = BattleAddMp(trust, damage);
                    }

                    if (primary && r.Resolution == 0)
                        SkillchainOnHit(trust, battleTarget, r, damage, ws.PrimarySc, ws.SecondarySc, ws.TertiarySc);
                }
            }
            else
            {
                // ActionInterrupts::WeaponSkillOutOfRange
                var oor = new ActionPacket { ActorId = trust.Id, ActionType = ActionCategory.MagicFinish };
                var r = oor.AddTarget(battleTarget.Id).AddResult();
                r.Animation = AnimWsInterrupt;
                r.MessageId = MsgTooFarAway;
                PushMobInRange(trust, Battle2Packets.Battle2(oor));
            }

            ProcessActionEffectFlags(trust, action);
        }

        // ---- a trust's ranged attack -------------------------------------------------------------------------------

        // CTrustController::RangedAttack: once its ranged weapon's delay (cmbDelay, taken as milliseconds as the C++
        // does) has passed since the last shot and it is not on the way somewhere: facing the target, the mob ranged
        // state (CAIContainer::Internal_RangedAttack) when the state can change
        private bool TrustRangedAttack(TrustEntity trust, BattleEntity target, long now)
        {
            long rangedDelay = trust.RangedBaseDelay;
            if (now - trust.LastTrustRangedMs > rangedDelay && !trust.InTransit)
            {
                if (target != null) FaceTarget(trust, target);
                if (target != null && CanChangeState(trust) && !IsUntargetable(target) && StartMobRanged(trust, target, now))
                    trust.LastTrustRangedMs = now;
                return true;
            }
            return false;
        }

        // ---- a mob's job ability (a trust's, useJobAbility's) -------------------------------------------------------

        // CTrustController::Ability / useJobAbility -> CAIContainer::Internal_Ability -> CAbilityState::init for a
        // mob: off its recast, the state free, a targetable target; the ability and a valid target, then (with a cast
        // time it can use) the "readies" BATTLE2 and the turn, ABILITY_START
        private bool MobAbility(MobEntity entity, BattleEntity target, ushort abilityId, long now)
        {
            if (RecastsOf(entity).HasRecast(RecastType.Ability, abilityId, 0)) return false;
            if (!CanChangeState(entity) || target == null || IsUntargetable(target)) return false;
            var ability = loader_.Abilities?.Get(abilityId);
            if (ability == null || !ValidSkillTarget(target, entity, ability.ValidTarget)) return false;
            var st = new AbilityState { Ability = ability.Clone(), Target = target, EntryMs = now, CastMs = ability.CastTimeMs };
            entity.AbilityState = st;
            if (st.CastMs > 0 && MobCanUseAbility(entity, st))
            {
                var action = new ActionPacket { ActorId = entity.Id, ActionType = ActionCategory.AbilityStart };
                var r = action.AddTarget(target.Id).AddResult();
                r.Animation = AnimAbilityStart;
                r.Param = ability.Id;
                r.MessageId = MsgAbilityReadies;
                PushMobInRange(entity, Battle2Packets.Battle2(action));
                TriggerListener(entity, "ABILITY_START", entity, ability);
                FaceTarget(entity, target);
            }
            else TriggerListener(entity, "ABILITY_START", entity, ability);
            return true;
        }

        // CAbilityState::CanUseAbility for a non-char: a target, no amnesia or job ability impairment, in range
        private bool MobCanUseAbility(MobEntity entity, AbilityState st)
        {
            var target = st.Target;
            var fx = entity.StatusEffects;
            var impairment = fx.GetStatusEffect(WsEffectImpairment);
            if (target == null || fx.HasStatusEffect(WsEffectAmnesia) || impairment != null && impairment.Power is 0x01 or 0x03) return false;
            if (ValidSkillTarget(target, entity, st.Ability.ValidTarget) && entity != target &&
                Distance(entity, target) > st.Ability.Range + HitboxSize(entity) + HitboxSize(target))
                return false;
            return true;
        }

        // the ability state on top of a mob's AI: true while there is one; once done, the suspended attack state
        // resumes where its swing timer stood
        private bool TickMobAbility(MobEntity entity, long now)
        {
            if (entity.AbilityState is not { } ja) return false;
            if (UpdateMobAbility(entity, ja, now))
            {
                entity.AbilityState = null;
                if (entity.Engaged) entity.SwingAtMs += now - ja.EntryMs;
            }
            return true;
        }

        // CAbilityState::Update for a mob: facing the target while it readies, the ability once the cast time is
        // over (a failed check ends the state at once), the packet and ABILITY_USE / ABILITY_TAKE, then out after the
        // animation; true once the state is done
        private bool UpdateMobAbility(MobEntity entity, AbilityState st, long now)
        {
            if (st.CastMs > 0 && now < st.EntryMs + st.CastMs && st.Target != null) FaceTarget(entity, st.Target);
            if (!st.Completed && now > st.EntryMs + st.CastMs)
            {
                // A failed validity check exits immediately and doesn't serve out the animation lock.
                if (!MobCanUseAbility(entity, st)) return true;
                var action = new ActionPacket();
                OnMobAbility(entity, st, action, now);
                TriggerListener(entity, "ABILITY_USE", entity, st.Target, st.Ability, action);
                if (action.Targets.Count > 0) PushMobInRange(entity, Battle2Packets.Battle2(action));
                foreach (var t in action.Targets)
                    if (TrustSkillTargetById(entity, t.ActorId) is { } pt)
                        TriggerListener(pt, "ABILITY_TAKE", entity, pt, st.Ability, action);
                st.Completed = true;
            }
            if (st.Completed && now > st.EntryMs + st.CastMs + st.Ability.AnimationTimeMs)
            {
                TriggerListener(entity, "ABILITY_STATE_EXIT", entity, st.Ability);
                return true;
            }
            return false;
        }

        // CBattleEntity::OnAbility for a mob: a valid target in range, paralysis, then the script on the target (or
        // every target around the mob for an AoE, the message varying after the first), its enmity, the recast;
        // processActionEffectFlags either way
        private void OnMobAbility(MobEntity entity, AbilityState st, ActionPacket action, long now)
        {
            var ability = st.Ability;
            var target = st.Target;
            if (target == null) return;
            if (ValidSkillTarget(target, entity, ability.ValidTarget))
            {
                if (entity != target && Distance(entity, target) > ability.Range + HitboxSize(entity) + HitboxSize(target)) return;
                if (IsParalyzed(entity))
                {
                    // ActionInterrupts::AbilityParalyzed
                    var paralyzed = new ActionPacket { ActorId = entity.Id, ActionType = ActionCategory.MagicFinish };
                    var pr = paralyzed.AddTarget(target.Id).AddResult();
                    pr.Animation = AnimAbilityInterrupt;
                    pr.MessageId = MsgAbilityIsParalyzed;
                    PushMobInRange(entity, Battle2Packets.Battle2(paralyzed));
                    return;
                }

                action.ActorId = entity.Id;
                action.ActionType = ability.ActionType;
                action.ActionId = ability.Id;
                action.RecastSec = (uint)(ability.RecastTimeMs / 1000);

                if (ability.IsAoE)
                {
                    var find = new SkillTargetFind(this, entity);
                    find.WithinArea(entity, AoeAttacker, ability.Radius, ability.ValidTarget);
                    ushort prevMsg = 0;
                    foreach (var found in find.Targets)
                    {
                        var r = action.AddTarget(found.Id).AddResult();
                        r.Resolution = 0; // ActionResolution::Hit
                        r.Animation = ability.Animation;
                        r.MessageId = ability.Message;
                        r.Param = 0;
                        int value = OnUseAbility(entity, found, ability, action);
                        r.MessageId = prevMsg == 0 ? ability.Message : AoEVariant(ability.Message);
                        r.Param = value;
                        if (value < 0)
                        {
                            r.MessageId = AbilityAbsorbVariant(r.MessageId);
                            r.Param = -r.Param;
                        }
                        prevMsg = r.MessageId;
                        MobAbilityApplyEnmity(entity, st, now);
                    }
                }
                else
                {
                    var r = action.AddTarget(target.Id).AddResult();
                    r.Resolution = 0;
                    r.Animation = ability.Animation;
                    ushort prevMsg = r.MessageId;
                    int value = OnUseAbility(entity, target, ability, action);
                    if (prevMsg == r.MessageId) r.MessageId = ability.Message;
                    if (r.MessageId == 0) r.MessageId = MsgUsesJobAbility;
                    r.Param = value;
                    if (value < 0)
                    {
                        r.MessageId = AbilityAbsorbVariant(r.MessageId);
                        r.Param = -value;
                    }
                }

                MobAbilityApplyEnmity(entity, st, now);
                RecastsOf(entity).Add(RecastType.Ability, ability.Id, ability.RecastTimeMs);
            }
            ProcessActionEffectFlags(entity, action);
        }

        // CAbilityState::ApplyEnmity for a mob: an enemy ability's CE / VE on the mob (and its claim for the
        // master), an ally's in-range enmity
        private void MobAbilityApplyEnmity(MobEntity entity, AbilityState st, long now)
        {
            var target = st.Target;
            if (target == null) return;
            var ability = st.Ability;
            int allegiance = EnmityContainer.AllegianceOf(target);
            if ((ability.ValidTarget & TargetEnemy) != 0 && allegiance != entity.Allegiance)
            {
                if (target is MobEntity enemy and not TrustEntity and not PetEntity && !(ability.CE == 0 && ability.VE == 0))
                {
                    enemy.Enmity.UpdateEnmity(entity, ability.CE, ability.VE, false);
                    ClaimMob(enemy, entity, now);
                }
            }
            else if (allegiance == entity.Allegiance)
            {
                GenerateInRangeEnmity(entity, ability.CE, ability.VE);
            }
        }
    }
}
