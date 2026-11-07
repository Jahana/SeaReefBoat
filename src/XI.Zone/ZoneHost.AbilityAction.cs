using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // GP_CLI_COMMAND_ACTION JobAbility (0x01A, ActionID 9): CPlayerController::Ability, CAbilityState and
    // CCharEntity::OnAbility for a char. The controller refuses an unknown ability (87) and one in recast (88 then 202
    // with the seconds left); init checks the target (applyTargetRestrictions) and, for an ability with a cast time,
    // pushes the "readies" BATTLE2 and turns towards the target. Once the cast time is over CanUseAbility checks the
    // recast (94), Amnesia / Impairment / the ability not learned (88), the range (78) and the script's onAbilityCheck,
    // then OnAbility works out the recast (merits, charges, Tabula Rasa, the Blood Pact delay, Reward's heads,
    // SIC_READY_RECAST), Paralysis, the invisible / Illusion removal, the AoE (xi.combat.abilityAoE), the script's
    // onUseAbility per target (OnSteal for Steal), the enmity and claim (ApplyEnmity), the mobs' onPlayerAbilityUse,
    // the cleanup effect, the recast (charutils::ApplyAbilityRecast) and processActionEffectFlags. The ABILITY_START /
    // ABILITY_USE / ABILITY_TAKE / ABILITY_STATE_EXIT listeners fire, and the state ends after the animation time.
    // The recast container comes along: charutils::LoadFromCharRecastSQL, the recasts BuildingCharAbilityTable's learn
    // adds, CRecastContainer::Check each tick, GP_SERV_COMMAND_ABIL_RECAST (0x119), the CLuaAbility type, the
    // GetAbility global and the recast bindings (addRecast, hasRecast, resetRecast, resetRecasts).
    // Pet abilities go to the pet (CharPetAbility, after PetSkillDistanceCheck and hasPetAbility), a TARGET_PET ability
    // to the char's pet, and the 0x01A case refuses a BST's Ready moves before Ready is learned or while the pet is not
    // engaged. A locked char (m_Locked) is no AoE target; Charm's enmity is tameable.
    // Not ported: BP Delay III (a TODO in the C++ too); turnTowardsTarget's UpdateEntityPacket (the position goes out
    // with the update mask).
    public sealed partial class ZoneHost
    {
        private const string AbilityType = "CAbility";

        // msg_basic.h
        private const ushort MsgAbilityIsParalyzed = 84, MsgUnableToUseJobAbility = 87, MsgUnableToUseJobAbility2 = 88,
            MsgAbilityWaitLonger = 94, MsgUsesJobAbility = 100, MsgAbilityTimeLeft = 202, MsgAbilityReadies = 326;

        // xi::StatusEffect ids EffectId lacks
        private const ushort AbilityEffectAmnesia = 16, AbilityEffectImpairment = 261, AbilityEffectSeigan = 354,
            AbilityEffectTabulaRasa = 377, AbilityEffectAvatarsFavor = 431;

        private const ushort MeritSicRecast = 902; // xi::Merit::SicRecast
        private const ushort AnimAbilityStart = 121, AnimAbilityInterrupt = 508; // ActionAnimation::SkillStart / SkillInterrupt
        private const byte FindFlagsDead = 0x01, FindFlagsAlliance = 0x02; // FINDFLAGS_DEAD, FINDFLAGS_ALLIANCE

        // CAbilityState
        internal sealed class AbilityState
        {
            public AbilityInfo Ability;
            public BattleEntity Target;
            public long EntryMs, CastMs;
            public bool Completed;
        }

        private readonly HashSet<string> abilityScripts_ = new HashSet<string>(StringComparer.Ordinal);

        // the GP_CLI_COMMAND_ACTION JobAbility case
        private void OnJobAbilityAction(MapSession s, CharData c, ushort actIndex, ushort abilityId)
        {
            // Don't allow BST to use ready before level 25
            if (c.PPet != null && (!AbilityLearned(c, AbilityInfo.Ready) || c.PPet is not MobEntity { Engaged: true }))
            {
                if (abilityId >= AbilityFootKick && abilityId <= AbilityPentapeck) // Is this a BST ability?
                {
                    Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgUnableToUseJobAbility2));
                    return;
                }
            }
            AbilityUse(s, c, actIndex, abilityId);
        }

        private const ushort AbilityFootKick = 672, AbilityPentapeck = 767; // ABILITY_FOOT_KICK .. ABILITY_PENTAPECK
        private const ushort AbilityCharm = 52; // ABILITY_CHARM

        // CPlayerController::Ability
        private void AbilityUse(MapSession s, CharData c, ushort targid, ushort abilityId)
        {
            // canAct and CanChangeState
            if (CanAct(c) && CanChangeState(c) && c.ItemUse == null)
            {
                var ability = loader_.Abilities?.Get(abilityId);
                if (ability == null)
                {
                    Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgUnableToUseJobAbility));
                    return;
                }
                var recasts = RecastsOf(c);
                if (recasts.HasRecast(RecastType.Ability, ability.RecastId, ability.RecastTimeMs))
                {
                    var recast = recasts.GetRecast(RecastType.Ability, ability.RecastId);
                    // Set recast time to the normal recast time minus any charge time.
                    // Abilities without a charge will have zero chargeTime
                    long currentRecast = recast.TimeStampMs - NowMs() + recast.RecastTimeMs;
                    // Abilities with a single charge (low-level scholar stratagems) behave like abilities without a charge
                    if (recast.MaxCharges > 1) currentRecast -= recast.ChargeTimeMs * (recast.MaxCharges - 1);
                    long seconds = AbilityCeilSeconds(currentRecast);
                    Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgUnableToUseJobAbility2));
                    Push(s, SelfPackets.BattleMessage(c, (int)Math.Max(seconds, 0), 0, MsgAbilityTimeLeft));
                    return;
                }
                if (AbilityEntityByTargId(c, targid) is BattleEntity abt && IsUntargetable(abt)) return;
                StartAbility(s, c, targid, abilityId);
                return;
            }
            Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgUnableToUseJobAbility));
        }

        // std::chrono::ceil<std::chrono::seconds> of a millisecond count
        private static long AbilityCeilSeconds(long ms) => (long)Math.Ceiling(ms / 1000.0);

        // PChar->GetEntity(targid) for a battle entity: the char itself, a mob of its zone, a char or a char's pet
        private BattleEntity AbilityEntityByTargId(CharData c, ushort targid) =>
            targid == c.TargId ? c : (BattleEntity)MobByTargId(c, targid) ?? (BattleEntity)CharByTargId(c, targid) ?? PetByTargId(c, targid);

        // a char's pet in the char's zone by its dynamic targid
        private PetEntity PetByTargId(CharData c, ushort targid) => AreaOf(c).Pets.Find(p => p.TargId == targid);

        // CCharEntity::IsValidTarget -> applyTargetRestrictions: the target when the ability may be used on it, else
        // null with the message to refuse with
        private BattleEntity AbilityValidTarget(CharData c, ushort targid, ushort validTarget, out BasicPacket errMsg) =>
            AbilityValidTarget(c, AbilityEntityByTargId(c, targid), validTarget, out errMsg);

        // CTargetFind::getValidTarget for a char: a TARGET_PET action goes to the char's pet, whatever was targeted;
        // else the target when the battle ids match (or are ignored) and ValidTarget allows it
        private BattleEntity GetValidTarget(CharData c, BattleEntity target, ushort validTarget)
        {
            if (target == null) return null;
            if ((validTarget & TargetPet) != 0 && c.PPet != null) return c.PPet;
            return c.StatusEffects.GetConfrontationEffect() == target.StatusEffects.GetConfrontationEffect() &&
                   c.PBattlefield == target.PBattlefield &&
                   ((validTarget & TargetIgnoreBattleId) == TargetIgnoreBattleId || c.BattleId == target.BattleId) &&
                   AbilityTargetValid(c, target, validTarget)
                ? target : null;
        }

        private BattleEntity AbilityValidTarget(CharData c, BattleEntity target, ushort validTarget, out BasicPacket errMsg)
        {
            errMsg = null;
            var validTargetFound = GetValidTarget(c, target, validTarget);
            if (validTargetFound != null)
            {
                target = validTargetFound;
                if (target is CharData tc && tc.BlockingAid && !InAllianceOf(tc, c))
                {
                    errMsg = SystemMessage(MsgStdTargetIsCurrentlyBlocking);
                    PushTo(tc, SystemMessage(MsgStdBlockedByBlockaid));
                    return null;
                }
                if (target is not MobEntity tm || IsMobOwner(c, tm))
                {
                    if (target.IsAlive || (validTarget & TargetPlayerDead) != 0) return target;
                    errMsg = SelfPackets.BattleMessage(c, 0, 0, MsgBasicCannotOnThatTarget);
                    return null;
                }
                errMsg = SelfPackets.BattleMessage(c.Id, c.TargId, tm.Id, tm.TargId, 0, 0, MsgAlreadyClaimed);
                return null;
            }
            errMsg = target is MobEntity pm && pm.Allegiance == CharData.AllegiancePlayer && (pm.Behavior & BehaviorNoAssist) != 0
                ? SelfPackets.BattleMessage(c, 0, 0, MsgBasicCannotOnThatTarget)
                : SelfPackets.BattleMessage(c, 0, 0, MsgBasicCannotAttackTarget);
            return null;
        }

        // CCharEntity::ValidTarget / CMobEntity's side of CBattleEntity::ValidTarget for a char initiator
        private static bool AbilityTargetValid(CharData c, BattleEntity target, ushort flags)
        {
            if (target is CharData tc) return SameArea(tc, c) && CharValidTarget(c, tc, flags);
            // CPetEntity::ValidTarget: no TARGET_PLAYER action on a pet of the char's side
            if (target is PetEntity pet && (flags & TargetPlayer) != 0 && pet.Allegiance == c.Allegiance) return false;
            var mob = (MobEntity)target;
            if (!mob.IsSpawned || !SameArea(mob, c)) return false;
            bool dead = !mob.IsAlive || mob.IsDead;
            // CBattleEntity::ValidTarget: an enemy of another allegiance (a mob is never the char itself)
            if ((flags & TargetEnemy) != 0 && !dead && mob.Allegiance != CharData.AllegiancePlayer) return true;
            if ((flags & TargetPlayerDead) != 0 && (mob.Behavior & BehaviorRaisable) != 0 && dead) return true;
            bool assists = mob.Allegiance == CharData.AllegiancePlayer && (mob.Behavior & BehaviorNoAssist) == 0;
            return (flags & (TargetPlayer | TargetNpc)) != 0 && assists;
        }

        // CAbilityState::init
        private void StartAbility(MapSession s, CharData c, ushort targid, ushort abilityId)
        {
            var ability = loader_.Abilities?.Get(abilityId);
            if (ability == null)
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgUnableToUseJobAbility));
                return;
            }
            var target = AbilityValidTarget(c, targid, ability.ValidTarget, out var errMsg);
            if (target == null || errMsg != null)
            {
                if (errMsg != null) Push(s, errMsg);
                return;
            }
            // the state keeps what was targeted (a TARGET_PET ability resolves to the pet only for the checks and the
            // readies packet)
            var st = new AbilityState { Ability = ability.Clone(), Target = AbilityEntityByTargId(c, targid), EntryMs = NowMs(), CastMs = ability.CastTimeMs };
            c.AbilityState = st;

            if (st.CastMs > 0 && AbilityCanUse(s, c, st))
            {
                var action = new ActionPacket { ActorId = c.Id, ActionType = ActionCategory.AbilityStart };
                var r = action.AddTarget(target.Id).AddResult();
                r.Animation = AnimAbilityStart;
                r.Param = ability.Id;
                r.MessageId = MsgAbilityReadies;
                PushCharInRange(c, Battle2Packets.Battle2(action), true);
                TriggerListener(c, "ABILITY_START", c, ability);

                // face toward target
                AbilityTurnTowards(c, target);
            }
            else
            {
                TriggerListener(c, "ABILITY_START", c, ability);
            }
        }

        // battleutils::turnTowardsTarget for a char (the 0x00D goes out with the update mask)
        private static void AbilityTurnTowards(CharData c, BattleEntity target)
        {
            c.Rotation = WorldAngle(c.X, c.Z, c.Rotation, target.X, target.Z);
            c.UpdateMask |= CharStats.UpdatePos;
        }

        // target().resolve(): the target while it is still in the world
        private BattleEntity AbilityTarget(AbilityState st) => st.Target != null && EntityExists(st.Target) ? st.Target : null;

        private void TickAbilities()
        {
            long now = NowMs();
            foreach (var (c, s) in new List<KeyValuePair<CharData, MapSession>>(charSessions_))
            {
                c.Recasts?.Check();
                var st = c.AbilityState;
                if (st == null) continue;
                // a char that died leaves the state stack (ClearStateStack)
                if (!c.IsAlive)
                {
                    c.AbilityState = null;
                    continue;
                }
                if (UpdateAbility(s, c, st, now))
                {
                    c.AbilityState = null;
                    // the suspended attack state resumes where its swing timer stood
                    if (c.AttackTarget != null) c.SwingAtMs += now - st.EntryMs;
                }
            }
        }

        // an ability in progress is dropped with the rest of the state stack (zoning, logout, a timeout, healing)
        private static void CancelAbility(CharData c) => c.AbilityState = null;

        // CAbilityState::Update: true once the state is done
        private bool UpdateAbility(MapSession s, CharData c, AbilityState st, long now)
        {
            // Rotate towards target during ability
            if (st.CastMs > 0 && now < st.EntryMs + st.CastMs && AbilityTarget(st) is { } facing)
                AbilityTurnTowards(c, facing);

            if (!st.Completed && now > st.EntryMs + st.CastMs)
            {
                // A failed validity check exits immediately and doesn't serve out the animation lock.
                if (!AbilityCanUse(s, c, st)) return true;

                var action = new ActionPacket();
                OnCharAbility(s, c, st, action);
                TriggerListener(c, "ABILITY_USE", c, AbilityTarget(st), st.Ability, action);
                // Only send packet if action was populated (e.g. interrupts return early)
                if (action.Targets.Count > 0) PushCharInRange(c, Battle2Packets.Battle2(action), true);
                foreach (var t in action.Targets)
                    if (WsBattleById(t.ActorId) is { } pt)
                        TriggerListener(pt, "ABILITY_TAKE", c, pt, st.Ability, action);
                st.Completed = true;
                SendUpdateMask(s, c);
            }

            if (st.Completed && now > st.EntryMs + st.CastMs + st.Ability.AnimationTimeMs)
            {
                c.History[HistoryAbilitiesUsed]++;
                TriggerListener(c, "ABILITY_STATE_EXIT", c, st.Ability);
                return true;
            }
            return false;
        }

        // charutils::hasAbility
        private static bool AbilityLearned(CharData c, ushort id) => id / 8 < c.Abilities.Length && (c.Abilities[id / 8] & 1 << (id % 8)) != 0;

        // CAbilityState::CanUseAbility for a char
        private bool AbilityCanUse(MapSession s, CharData c, AbilityState st)
        {
            var ability = st.Ability;
            var target = AbilityTarget(st);
            bool isLuopanAbility = ability.Id >= AbilityInfo.ConcentricPulse && ability.Id <= AbilityInfo.RadialArcana;

            if (RecastsOf(c).HasRecast(RecastType.Ability, ability.RecastId, ability.RecastTimeMs))
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgAbilityWaitLonger));
                return false;
            }

            var impairment = c.StatusEffects.GetStatusEffect(AbilityEffectImpairment);
            if (c.StatusEffects.HasStatusEffect(AbilityEffectAmnesia) ||
                impairment != null && (impairment.Power == 0x01 || impairment.Power == 0x03) ||
                (!ability.IsPetAbility || isLuopanAbility) && !AbilityLearned(c, ability.Id) ||
                ability.IsPetAbility && ability.Id >= AbilityInfo.HealingRuby && !HasPetAbility(c, ability.Id - AbilityInfo.HealingRuby))
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgUnableToUseJobAbility2));
                return false;
            }

            if (target != null && AbilityValidTarget(c, target == c ? c.TargId : target.TargId, ability.ValidTarget, out _) != null)
            {
                // blood pacts and ready moves check their distances apart (PetSkillDistanceCheck)
                var petSkill = ability.IsPetAbility ? GetPetSkill(ability.Id) : null;
                if (petSkill != null && !isLuopanAbility && (petSkill.IsBloodPactRage || petSkill.IsBloodPactWard || petSkill.MobSkillId > 0))
                {
                    if (!PetSkillDistanceCheck(s, c, target, ability)) return false;
                }
                else if (c != target && Distance(c, target) > ability.Range + HitboxSize(c) + HitboxSize(target))
                {
                    Push(s, SelfPackets.BattleMessage(c.Id, c.TargId, target.Id, target.TargId, 0, 0, MsgTooFarAway));
                    return false;
                }

                if (ZoneInfoOf(c.Zone).CanUseMisc("los_player_block") && !CanSeeTarget(c, target))
                {
                    Push(s, SelfPackets.BattleMessage(c.Id, c.TargId, target.Id, target.TargId, 0, 0, MsgCannotSee));
                    return false;
                }
                BattleEntity msgTarget = c;
                int errNo = OnAbilityCheck(c, target, ability, ref msgTarget);
                if (errNo != 0)
                {
                    Push(s, SelfPackets.BattleMessage(c.Id, c.TargId, msgTarget.Id, msgTarget.TargId, ability.Id, ability.Id, (ushort)errNo));
                    return false;
                }
                return true;
            }
            return false;
        }

        // CCharEntity::OnAbility
        private void OnCharAbility(MapSession s, CharData c, AbilityState st, ActionPacket action)
        {
            var ability = st.Ability;
            var recasts = RecastsOf(c);
            if (recasts.HasRecast(RecastType.Ability, ability.RecastId, ability.RecastTimeMs))
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgAbilityWaitLonger));
                return;
            }
            if (c.StatusEffects.HasStatusEffect(AbilityEffectAmnesia))
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgUnableToUseJobAbility2));
                return;
            }

            byte findFlags = 0;
            if ((ability.ValidTarget & TargetPlayerDead) == TargetPlayerDead) findFlags |= FindFlagsDead;

            var target = AbilityTarget(st);
            // Check if target is untargetable
            if (target == null || !AbilityValidEntity(c, target, findFlags, null)) return;

            if (AbilityValidTarget(c, target == c ? c.TargId : target.TargId, ability.ValidTarget, out var errMsg) != null)
            {
                // get any available recast reduction
                long recastReduction = 0; // seconds
                if (ability.MeritModId > 0 && (ability.AddType & AbilityTable.AddTypeMerit) == 0)
                {
                    recastReduction = c.Merits?.Value(c, ability.MeritModId) ?? 0;
                    if (ability.Id == AbilityInfo.ThirdEye && c.StatusEffects.HasStatusEffect(AbilityEffectSeigan))
                        recastReduction /= 2;
                }

                var charge = loader_.Abilities?.GetCharge(c, ability.RecastId);
                long baseChargeTimeMs = 0; // this can be reduced with merits/job point gifts. NOT the same as Recast- gear (so far...)
                long bloodPactRecastMs = 0;
                long recastMs;

                if (charge != null && ability.Id != AbilityInfo.Sic)
                {
                    long chargesUsed = ability.RecastTimeMs / 1000; // charge cost is stored in the recast...

                    //  Can't assign merits via ability ID for Sic/Ready due to shenanigans
                    if (ability.RecastId == Recast.Sic) recastReduction = c.Merits?.Value(c, MeritSicRecast) ?? 0;
                    else if (ability.RecastId == Recast.Strategems) recastReduction += c.GetMod(Mod.STRATAGEM_RECAST);

                    baseChargeTimeMs = charge.ChargeTimeMs - recastReduction * 1000;
                    recastMs = baseChargeTimeMs * chargesUsed;
                }
                else
                {
                    recastMs = ability.RecastTimeMs - recastReduction * 1000;
                }

                if (ability.Id == AbilityInfo.LightArts || ability.Id == AbilityInfo.DarkArts || ability.RecastId == Recast.Strategems)
                {
                    if (c.StatusEffects.HasStatusEffect(AbilityEffectTabulaRasa))
                    {
                        recastMs = 0;
                        baseChargeTimeMs = 0;
                    }
                }
                else if (ability.RecastId == Recast.BloodPactRage || ability.RecastId == Recast.BloodPactWard)
                {
                    int favorReduction = 0;
                    int bloodPactIReduction = Math.Min((int)c.GetMod(Mod.BP_DELAY), 15);
                    int bloodPactIIReduction = Math.Min((int)c.GetMod(Mod.BP_DELAY_II), 15);
                    // BP Delay III (SMN JP gift) not implemented
                    if (c.StatusEffects.GetStatusEffect(AbilityEffectAvatarsFavor) is { } avatarsFavor)
                        favorReduction = Math.Min((int)avatarsFavor.Power, 10);

                    int bloodPactDelayReduction = favorReduction + Math.Min(bloodPactIReduction + bloodPactIIReduction, 30);

                    // Snapshot BP recast here so we can carry it into Paralyze check
                    bloodPactRecastMs = Math.Max(0, recastMs - bloodPactDelayReduction * 1000L);

                    // Localvar will set the BP ability timer when the move consumes MP
                    c.LocalVars["bpRecastTime"] = (ushort)(bloodPactRecastMs / 1000);

                    // Recast is actually triggered when the bp goes off
                    recastMs = 0;
                }

                // Check paralysis and consume recast for non-SP abilities (battleutils::IsParalyzed)
                if (MobRng.Next(100) < c.GetMod(Mod.PARALYZE))
                {
                    // SP abilities don't consume recast when paralyzed
                    ushort recastId = ability.RecastId;
                    if (recastId != Recast.Special && recastId != Recast.Special2)
                    {
                        bool isBloodPact = recastId == Recast.BloodPactRage || recastId == Recast.BloodPactWard;
                        ApplyAbilityRecast(c, ability, charge, baseChargeTimeMs, isBloodPact ? bloodPactRecastMs : recastMs);
                    }

                    // ActionInterrupts::AbilityParalyzed
                    var paralyzed = new ActionPacket { ActorId = c.Id, ActionType = ActionCategory.MagicFinish };
                    var pr = paralyzed.AddTarget(target.Id).AddResult();
                    pr.Animation = AnimAbilityInterrupt;
                    pr.MessageId = MsgAbilityIsParalyzed;
                    PushCharInRange(c, Battle2Packets.Battle2(paralyzed), true);
                    return;
                }

                // remove invisible if aggressive
                if (ability.Id != AbilityInfo.Tame && ability.Id != AbilityInfo.Fight && ability.Id != AbilityInfo.Deploy && ability.Id != AbilityInfo.Gauge)
                {
                    if ((ability.ValidTarget & TargetEnemy) != 0)
                    {
                        // charutils::RemoveInvisible for Assault, else the generic aggressive action
                        if (ability.Id == AbilityInfo.Assault) c.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Invisible);
                        else c.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Detectable);
                        c.StatusEffects.DelStatusEffect(EffectId.Illusion);
                    }
                    else if (ability.Id != AbilityInfo.TrickAttack)
                    {
                        // remove invisible only
                        c.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Invisible);
                        c.StatusEffects.DelStatusEffect(EffectId.Illusion);
                    }
                }

                if (ability.Id == AbilityInfo.Reward)
                {
                    // The Bison/Brave's Warbonnet & Khimaira/Stout Bonnet reduces recast time by 10 seconds.
                    ushort head = CharStats.EquippedItem(c, 4)?.ItemId ?? 0; // SLOT_HEAD
                    if (head == 15157 || head == 15158 || head == 16104 || head == 16105) recastMs -= 10000;
                }
                else if (ability.Id == AbilityInfo.Ready || ability.Id == AbilityInfo.Sic)
                {
                    recastMs = Math.Max(0, recastMs - c.GetMod(Mod.SIC_READY_RECAST) * 1000L);
                }

                action.ActorId = c.Id;
                action.ActionType = ability.ActionType;
                action.ActionId = ability.Id;
                // action.recast as the scripts see it (CLuaAction getRecast / setRecast)
                uint recastSec = (uint)Math.Max(0, recastMs / 1000);
                action.RecastSec = recastSec;

                // Calculate ability AoE type and radius via Lua
                var (aoeType, aoeRadius) = AbilityAoE(c, ability);

                if (ability.IsPetAbility)
                {
                    // TODO: get rid of this to script, too
                    CharPetAbility(c, target, ability, action);
                }
                else if (aoeType != 0)
                {
                    var targets = AbilityFindWithinArea(c, aoeRadius, findFlags, ability.ValidTarget);
                    ushort prevMsg = 0;
                    foreach (var found in targets)
                    {
                        var r = action.AddTarget(found.Id).AddResult();
                        r.Resolution = 0; // ActionResolution::Hit
                        r.Animation = ability.Animation;
                        r.MessageId = ability.Message;

                        int value = OnUseAbility(c, found, ability, action);

                        // the default message for the first target, the AoE message for the others
                        r.MessageId = prevMsg == 0 ? ability.Message : AoEVariant(ability.Message);
                        r.Param = value;
                        if (value < 0)
                        {
                            r.MessageId = AbilityAbsorbVariant(r.MessageId);
                            r.Param = -r.Param;
                        }
                        prevMsg = r.MessageId;

                        AbilityApplyEnmity(c, st);
                    }
                }
                else
                {
                    var r = action.AddTarget(target.Id).AddResult();
                    r.Resolution = 0; // ActionResolution::Hit
                    r.Animation = ability.Animation;
                    ushort prevMsg = r.MessageId;

                    // Check for special situations from Steal (The Tenshodo Showdown quest)
                    if (ability.Id == AbilityInfo.Steal) r.Param = OnSteal(c, target, ability, action);

                    int value = OnUseAbility(c, target, ability, action);

                    if (prevMsg == r.MessageId) r.MessageId = ability.Message;
                    // TODO: Some abilities legitimately have no message (e.g., Full Circle)
                    if (r.MessageId == 0) r.MessageId = MsgUsesJobAbility;

                    r.Param = value;
                    if (value < 0)
                    {
                        r.MessageId = AbilityAbsorbVariant(r.MessageId);
                        r.Param = -value;
                    }

                    AbilityApplyEnmity(c, st);
                }

                // a script's setRecast replaces the recast
                if (action.RecastSec != recastSec) recastMs = action.RecastSec * 1000L;

                // Some mobs respond to abilities (ex. Absolute Virtue / Ob)
                foreach (var m in c.Notoriety)
                    if (m.GetMobMod(MobMod.AbilityResponse) != 0 && SameArea(m, c))
                        CallMobHook(m, "onPlayerAbilityUse", m, c, ability);

                // Cleanup "consumed" abilities after action like Contradance
                c.StatusEffects.DelStatusEffect(ability.CleanupEffect);

                ApplyAbilityRecast(c, ability, charge, baseChargeTimeMs, recastMs);
                ProcessActionEffectFlags(c, action);
            }
            else if (errMsg != null)
            {
                Push(s, errMsg);
            }
        }

        // messageutils::GetAbsorbVariant
        private static ushort AbilityAbsorbVariant(ushort msg) => msg switch
        {
            110 => 102, // UsesAbilityTakesDamage -> UsesRecoversHP
            264 => 263, // TargetTakesDamage -> TargetRecoversHP2
            _ => msg,
        };

        // charutils::ApplyAbilityRecast
        private void ApplyAbilityRecast(CharData c, AbilityInfo ability, AbilityCharge charge, long baseChargeTimeMs, long recastMs)
        {
            var recasts = RecastsOf(c);
            if (charge != null) recasts.Add(RecastType.Ability, ability.RecastId, recastMs, baseChargeTimeMs, charge.MaxCharges);
            else recasts.Add(RecastType.Ability, ability.RecastId, recastMs);

            ushort recastId = ability.RecastId;
            if (ChatCfg.BloodPactSharedTimer && (recastId == Recast.BloodPactRage || recastId == Recast.BloodPactWard))
                recasts.Add(RecastType.Ability, recastId == Recast.BloodPactRage ? Recast.BloodPactWard : Recast.BloodPactRage, recastMs);

            // Yonin (recastId 146) and Innin share a server-side timer via the SQL recastId update.
            // Also add Innin's original client-facing recast ID (147) so the client greys out Innin.
            if (recastId == 146) recasts.Add(RecastType.Ability, 147, recastMs);

            PushTo(c, AbilRecast(c));
        }

        // CLuaBaseEntity::resetRecast for a char: a recast held goes back to 0, then CLISTATUS2 and ABIL_RECAST
        private void ResetRecast(CharData c, RecastType type, ushort id)
        {
            var recasts = RecastsOf(c);
            if (recasts.Has(type, id))
            {
                recasts.Del(type, id);
                recasts.Add(type, id, 0);
            }
            PushTo(c, SelfPackets.CliStatus2(c));
            PushTo(c, AbilRecast(c));
        }

        // CAbilityState::ApplyEnmity
        private void AbilityApplyEnmity(CharData c, AbilityState st)
        {
            var target = AbilityTarget(st);
            if (target == null) return;
            var ability = st.Ability;
            int allegiance = EnmityContainer.AllegianceOf(target);
            if ((ability.ValidTarget & TargetEnemy) != 0 && allegiance != CharData.AllegiancePlayer)
            {
                if (target is MobEntity mob && !(ability.CE == 0 && ability.VE == 0))
                {
                    mob.Enmity.UpdateEnmity(c, ability.CE, ability.VE, withMaster: false, tameable: ability.Id == AbilityCharm);
                    ClaimMob(mob, c, NowMs());
                }
            }
            else if (allegiance == CharData.AllegiancePlayer)
            {
                GenerateInRangeEnmity(c, ability.CE, ability.VE);
            }
        }

        // CTargetFind::validEntity for a char's ability: around is null for a single target (always taken once it
        // passes the common checks), else the caster's position for the self-centered AoE
        private static bool AbilityValidEntity(CharData c, BattleEntity t, byte findFlags, List<BattleEntity> targets,
            ushort targetFlags = 0, float radius = 0)
        {
            if (t == null || targets != null && targets.Contains(t)) return false;
            bool dead = t is MobEntity tm ? tm.IsDead || !tm.IsSpawned || !tm.IsAlive : !t.IsAlive;
            if ((findFlags & FindFlagsDead) == 0 && dead) return false;
            if (c.StatusEffects.GetConfrontationEffect() != t.StatusEffects.GetConfrontationEffect() || c.PBattlefield != t.PBattlefield ||
                (findFlags & FindFlagsIgnoreBattleId) == 0 && c.BattleId != t.BattleId) return false;
            if (!SameArea(t, c) || IsUntargetable(t)) return false;
            // m_Locked targets should not be able to be attacked or have any ability or spell cast on them, including AoEs.
            if (t is CharData { Locked: true }) return false;
            if (targets == null) return true;

            // Benediction/self-centered ally-only check
            if ((targetFlags & (TargetAnyAllegiance | TargetEnemy)) == 0 && EnmityContainer.AllegianceOf(t) != CharData.AllegiancePlayer) return false;
            // check vertical range
            if (MathF.Abs(t.Y - c.Y) >= 8.0f) return false;
            // If offensive, don't target other entities with same allegiance
            if ((targetFlags & TargetAnyAllegiance) == 0 && (targetFlags & TargetEnemy) != 0 && (targetFlags & TargetPlayerParty) == 0 &&
                EnmityContainer.AllegianceOf(t) == CharData.AllegiancePlayer)
                return false;
            return Distance(c, t) <= radius;
        }

        // CTargetFind::findWithinArea(this, AOE_RADIUS::ATTACKER, ...) for a char: PLAYER_PLAYER, the caster first,
        // then its party members not in a mog house and their trusts (addAllInParty's ForPartyWithTrusts), or the
        // caster alone; no pets (PETS_CAN_AOE_BUFF is false, the caster is its own master target)
        private List<BattleEntity> AbilityFindWithinArea(CharData c, float radius, byte findFlags, ushort targetFlags)
        {
            var targets = new List<BattleEntity>();
            void Add(BattleEntity t)
            {
                if (AbilityValidEntity(c, t, findFlags, targets, targetFlags, radius)) targets.Add(t);
            }
            Add(c);
            if (c.Party != null)
            {
                foreach (var member in PartyWithTrusts(c))
                    if (member is not CharData { MoghouseId: not 0 }) Add(member);
            }
            else
            {
                Add(c);
            }
            return targets;
        }

        private string AbilityScriptFile(AbilityInfo ability) => ability.IsPetAbility
            ? $"./scripts/actions/abilities/pets/{ability.Name}.lua"
            : $"./scripts/actions/abilities/{ability.Name}.lua";

        private void AbilityLoadScript(string file)
        {
            if (scripts_ != null && abilityScripts_.Add(file)) scripts_.LoadLuaObjectFromFile(file);
        }

        // luautils::OnAbilityCheck: the message number (0 lets the ability go); a second result other than 0 sends
        // the message to the target
        private int OnAbilityCheck(CharData c, BattleEntity t, AbilityInfo ability, ref BattleEntity msgTarget)
        {
            if (lua_ == null) return 0;
            string file = AbilityScriptFile(ability);
            AbilityLoadScript(file);
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(file, "onAbilityCheck");
                // We rely on this to fail silently in certain cases
                if (lua_.IsNil(-1)) return 0;
                lua_.Push(c);
                lua_.Push(t);
                lua_.Push(ability);
                lua_.Call(3, 2);
                int result0 = lua_.IsNumber(-2) ? (int)lua_.ToIntegral(-2) : 0; // Message (0 = None)
                int result1 = lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : 0;
                if (result1 != 0) msgTarget = t;
                return result0;
            }
            catch (LuaException e)
            {
                log_($"luautils::onAbilityCheck ({ability.Name}): {e.Message}");
                return 87;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnUseAbility
        private int OnUseAbility(BattleEntity c, BattleEntity t, AbilityInfo ability, ActionPacket action)
        {
            if (lua_ == null) return 0;
            string file = AbilityScriptFile(ability);
            AbilityLoadScript(file);
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(file, "onUseAbility");
                if (lua_.IsNil(-1))
                {
                    log_($"luautils::onUseAbility - Ability {ability.Name} not found.");
                    return 0;
                }
                lua_.Push(c);
                lua_.Push(t);
                lua_.Push(ability);
                lua_.Push(action);
                lua_.Call(4, 1);
                return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : 0;
            }
            catch (LuaException e)
            {
                log_($"luautils::onUseAbility: {e.Message}");
                return 0;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnSteal: InteractionGlobal.onSteal(player, mob, ability, action, the mob script's onSteal)
        private int OnSteal(CharData c, BattleEntity mob, AbilityInfo ability, ActionPacket action)
        {
            if (lua_ == null || mob == null) return 0;
            string file = $"./scripts/zones/{AreaOf(c).Name}/mobs/{mob.Name}.lua";
            int top = lua_.Top;
            try
            {
                lua_.GetPath("InteractionGlobal.onSteal");
                lua_.Push(c);
                lua_.Push(mob);
                lua_.Push(ability);
                lua_.Push(action);
                scripts_.PushFileFunction(file, "onSteal");
                lua_.Call(5, 1);
                ushort retVal = lua_.IsNumber(-1) ? (ushort)lua_.ToIntegral(-1) : (ushort)0;
                return retVal > 0 ? retVal : 0;
            }
            catch (LuaException e)
            {
                log_($"luautils::onSteal {e.Message}");
                return 0;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::callGlobal("xi.combat.abilityAoE.calculateTypeAndRadius", player, ability): { type, radius }
        private (byte Type, float Radius) AbilityAoE(CharData c, AbilityInfo ability)
        {
            if (lua_ == null) return (0, 0);
            const string path = "xi.combat.abilityAoE.calculateTypeAndRadius";
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (lua_.IsNil(-1))
                {
                    log_($"luautils::callGlobalFunction: {path}: Function not found");
                    return (0, 0);
                }
                lua_.Push(c);
                lua_.Push(ability);
                lua_.Call(2, 1);
                if (!lua_.IsTable(-1)) return (0, 0);
                lua_.RawGetI(-1, 1);
                byte type = lua_.IsNumber(-1) ? (byte)lua_.ToIntegral(-1) : (byte)0;
                lua_.Pop(1);
                lua_.RawGetI(-1, 2);
                float radius = lua_.IsNumber(-1) ? (float)lua_.ToNumber(-1) : 0f;
                return (type, radius);
            }
            catch (LuaException e)
            {
                ScriptError(path, e);
                return (0, 0);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // ---- the recast container ----------------------------------------------------------------------------------

        // the entity's PRecastContainer (a CCharRecastContainer for a char)
        private RecastContainer RecastsOf(BattleEntity b) =>
            b.Recasts ??= b is CharData c ? NewCharRecasts(c) : new RecastContainer(() => NowMs(), log_);

        private CharRecastContainer NewCharRecasts(CharData c) =>
            new CharRecastContainer(c.Id, (q, a) => Exec(q, a), () => NowMs(), () => UnixNow(), id => AbilityItemReady(c, id), log_);

        // CCharRecastContainer::Check's item refresh: ITEM_ATTR for the item at (slot << 8 | container), then ITEM_SAME
        private void AbilityItemReady(CharData c, ushort id)
        {
            byte slot = (byte)(id >> 8 & 0xFF);
            var container = (Container)(id & 0xFF);
            foreach (var item in c.Items)
            {
                if (item.Location == container && item.Slot == slot)
                {
                    PushTo(c, GameOkPackets.ItemAttr(item, GameOkPackets.LockFlagFor(c, item)));
                    break;
                }
            }
            PushTo(c, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
        }

        internal const string SqlRecasts = "SELECT id, time, recast FROM char_recast WHERE charid = ?";

        // charutils::LoadFromCharRecastSQL (the CharLoader's AfterLevels hook): the ability recasts still running
        private void LoadCharRecasts(CharData c)
        {
            var recasts = NewCharRecasts(c);
            c.Recasts = recasts;
            var r = Exec(SqlRecasts, BoundValue.Of(c.Id));
            while (r != null && r.Next())
            {
                long now = UnixNow();
                long castTime = r.Get<uint>("time");
                long recast = r.Get<uint>("recast");
                ushort id = r.Get<ushort>("id");
                var charge = loader_.Abilities?.GetCharge(c, id);
                if (now < castTime + recast)
                    recasts.Load(RecastType.Ability, id, (castTime + recast - now) * 1000, charge?.ChargeTimeMs ?? 0, charge?.MaxCharges ?? 0);
            }
        }

        // BuildingCharAbilityTable's learn (the CharLoader's AbilityLearned hook): a charge ability's charges, or an
        // empty recast for one not listed yet
        private void AbilityLearnedRecast(CharData c, AbilityTable.Ability learned)
        {
            var ability = loader_.Abilities?.Get(learned.Id);
            if (ability == null) return;
            var charge = loader_.Abilities.GetCharge(c, ability.RecastId);
            long chargeTimeMs = 0;
            byte maxCharges = 0;
            if (charge != null)
            {
                chargeTimeMs = charge.ChargeTimeMs - (c.Merits?.Value(c, charge.Merit) ?? 0) * 1000L;
                maxCharges = charge.MaxCharges;
            }
            var recasts = RecastsOf(c);
            if (charge != null || !recasts.Has(RecastType.Ability, ability.RecastId))
                recasts.Add(RecastType.Ability, ability.RecastId, 0, chargeTimeMs, maxCharges);
        }

        // 0x119 GP_SERV_COMMAND_ABIL_RECAST: Timers[31] at 4 (Timer u16, Calc1 u8, TimerId u8, Calc2 u16, 8 bytes
        // each), MountRecast u32 at 252, MountRecastId u32 at 256. Timers[0] is the two-hour.
        private BasicPacket AbilRecast(CharData c)
        {
            var w = new PacketWriter(0x119, 260);
            int count = 1;
            long now = NowMs();
            foreach (var recast in RecastsOf(c).GetRecastList(RecastType.Ability))
            {
                long remaining = recast.RecastTimeMs == 0 ? 0 : AbilityCeilSeconds(recast.TimeStampMs - now + recast.RecastTimeMs);
                uint recastSeconds = (uint)Math.Max(remaining, 0);

                if (recast.Id == Recast.Mount) // borrowing this id for mount recast
                {
                    w.U32(252, recastSeconds);
                    w.U32(256, recast.Id);
                }
                else if (recast.Id != Recast.Special)
                {
                    int at = 4 + 8 * count;
                    w.U16(at, (ushort)recastSeconds);
                    w.U8(at + 3, (byte)recast.Id);
                    if (recast.MaxCharges != 0 && loader_.Abilities?.GetCharge(c, recast.Id) is { } charge)
                    {
                        ushort actualChargeTime = (ushort)(recast.ChargeTimeMs / 1000);
                        ushort baseChargeTime = (ushort)(charge.ChargeTimeMs / 1000);
                        if (baseChargeTime > actualChargeTime)
                        {
                            w.U8(at + 2, 0); // Not used in Ready, QD, Stratagems... Is this never used?
                            w.U16(at + 4, (ushort)(65536 - (baseChargeTime - actualChargeTime) * recast.MaxCharges));
                        }
                    }
                    count++;
                }
                else // 2hr edge case
                {
                    w.U16(4, (ushort)recastSeconds);
                    w.U8(7, 0);
                }

                // Retail currently only allows 31 distinct recasts to be sent in the packet
                if (count > 30)
                {
                    log_($"GP_SERV_COMMAND_ABIL_RECAST constructor attempting to send recast packet to player '{c.Name}' with > 31 abilities. This is unsupported.");
                    break;
                }
            }
            return w.Packet;
        }

        // ---- bindings ----------------------------------------------------------------------------------------------

        private void RegisterAbilityBindings(Dictionary<string, LuaFunction> m)
        {
            // CLuaAbility
            static AbilityInfo Ability(LuaState l) => l.ToObject(1) as AbilityInfo ?? throw new InvalidOperationException("bad self: expected " + AbilityType);
            lua_.RegisterType(AbilityType, new Dictionary<string, LuaFunction>
            {
                ["getID"] = l => Ret(l, Ability(l).Id),
                ["getMsg"] = l => Ret(l, Ability(l).Message),
                ["getRecast"] = l => Ret(l, (ushort)(Ability(l).RecastTimeMs / 1000)),
                ["getRecastID"] = l => Ret(l, Ability(l).RecastId),
                ["getRange"] = l => Ret(l, (ushort)Ability(l).Range),
                ["getRadius"] = l => Ret(l, Ability(l).Radius),
                ["getAOE"] = l => Ret(l, Ability(l).Aoe),
                ["getName"] = l =>
                {
                    l.PushString(Ability(l).Name);
                    return 1;
                },
                ["getAnimation"] = l => Ret(l, Ability(l).Animation),
                ["getAddType"] = l => Ret(l, Ability(l).AddType),
                ["setMsg"] = l =>
                {
                    Ability(l).Message = (ushort)l.ToEnumArg(2); // MsgBasic
                    return 0;
                },
                ["setAnimation"] = l =>
                {
                    Ability(l).Animation = (ushort)l.ToIntArg(2);
                    return 0;
                },
                ["setRecast"] = l =>
                {
                    Ability(l).SetRecastTime((ushort)l.ToIntArg(2) * 1000L);
                    return 0;
                },
                ["getCE"] = l => Ret(l, Ability(l).CE),
                ["setCE"] = l =>
                {
                    Ability(l).CE = l.ToIntArg(2);
                    return 0;
                },
                ["getVE"] = l => Ret(l, Ability(l).VE),
                ["setVE"] = l =>
                {
                    Ability(l).VE = l.ToIntArg(2);
                    return 0;
                },
                ["setRange"] = l =>
                {
                    Ability(l).Range = (float)l.ToNumberArg(2);
                    return 0;
                },
                ["setPostActionCleanupEffect"] = l =>
                {
                    Ability(l).CleanupEffect = (ushort)l.ToEnumArg(2); // xi::StatusEffect
                    return 0;
                },
            });

            // luautils::GetAbility
            lua_.SetGlobal("GetAbility", l =>
            {
                l.Push(loader_.Abilities?.Get((ushort)l.ToIntArg(1)));
                return 1;
            });

            // CLuaBaseEntity::addRecast(container, recastID, duration seconds)
            m["addRecast"] = l =>
            {
                if (Entity(l) is BattleEntity b)
                {
                    RecastsOf(b).Add((RecastType)(byte)l.ToIntArg(2), (ushort)l.ToIntArg(3), l.ToUIntArg(4) * 1000L);
                    if (b is CharData c)
                    {
                        PushTo(c, SelfPackets.CliStatus2(c));
                        PushTo(c, AbilRecast(c));
                    }
                }
                return 0;
            };

            // CLuaBaseEntity::hasRecast(container, recastID, recast seconds (optional, the charges))
            m["hasRecast"] = l =>
            {
                bool has = false;
                if (Entity(l) is BattleEntity b)
                {
                    long recastMs = l.IsNil(4) ? 0 : l.ToUIntArg(4) * 1000L;
                    has = RecastsOf(b).HasRecast((RecastType)(byte)l.ToIntArg(2), (ushort)l.ToIntArg(3), recastMs);
                }
                return Ret(l, has);
            };

            // CLuaBaseEntity::resetRecast: only reset for players
            m["resetRecast"] = l =>
            {
                if (Entity(l) is CharData c) ResetRecast(c, (RecastType)(byte)l.ToIntArg(2), (ushort)l.ToIntArg(3));
                return 0;
            };

            // CLuaBaseEntity::resetRecasts: only reset for players
            m["resetRecasts"] = l =>
            {
                if (Entity(l) is CharData c)
                {
                    var recasts = RecastsOf(c);
                    recasts.Del(RecastType.Magic);
                    recasts.Del(RecastType.Ability);
                    PushTo(c, SelfPackets.CliStatus2(c));
                    PushTo(c, AbilRecast(c));
                }
                return 0;
            };
        }
    }
}
