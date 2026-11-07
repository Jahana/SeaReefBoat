using System;
using System.Collections.Generic;
using XI.Map;

namespace XI.Zone
{
    // CMobController's fight: aggro (CZoneEntities::tapMobAggro, CanAggroTarget), DoRoamTick turning to whoever is on
    // the enmity list (or the claimer), resting and leaving neutral, DoCombatTick (the stale claim, HandleEnmity,
    // TryDeaggro, Move) and the mob's CAttackState, its swings landing on a char through
    // battleutils::TakePhysicalDamage. A char at 0 HP dies (CCharEntity::Die) and an hour later is sent home.
    // A mob pet engages with its master, never aggroes on its own, and the special skill goes first in combat
    // (ZoneHost.MobPets.cs).
    // The chase (Move) walks the zone's navmesh (PathFind.cs) with the collision mesh's line of sight (ZoneHost.XiMesh.cs).
    // Spells are in ZoneHost.MobMagic.cs, skills in ZoneHost.MobSkills.cs, ranged attacks in ZoneHost.MobRanged.cs,
    // following in ZoneHost.MobFollow.cs, the script hooks (onMobEngage, onMobFight, onMobRoam...) in
    // ZoneHost.MobScripts.cs and a dead char's death-time save in ZoneHost.cs (SaveDeathTime). Its swings meet the char's
    // reactions (parry, shadows, counters, guard, block) through MeleeSwing. Roaming, walking home and following a
    // path are in ZoneHost.MobRoam.cs.
    public sealed partial class ZoneHost
    {
        private const long MobAiTickMs = 400;              // kLogicUpdateInterval
        private const long MobEngageDelayMs = 3000;        // CAttackState's kEngageDelay
        private const long MobDeaggroMs = 25000;           // CheckDetection: 25 s since the mob last noticed its target
        private const long CharDeathDurationMs = 3600_000; // CCharEntity::death_duration
        private const long CharDeathSyncMs = 16000;        // CCharEntity::death_update_frequency
        private const long MobNeutralMs = 15000;           // kNeutralDuration
        private const long MobRoamScriptMs = 3000;         // DoRoamTick: onMobRoam and ROAM_TICK at most every 3 s
        private const long MobRestIntervalMs = 10000;      // DoRoamTick: 10% HP every 10 s idle
        private const ushort FamilyFomor = 172, ZoneLufaiseMeadows = 24, ZoneSacrarium = 28;
        private const ushort MsgPlayerDefeatedBy = 97;
        private const ushort BehaviorNoTurn = 0x400, BehaviorAggroAmbush = 0x200;
        private const int DetectsSight = 0x001, DetectsHearing = 0x002, DetectsLowHp = 0x004, DetectsMagic = 0x020, DetectsAbility = 0x040,
            DetectsScent = 0x100;

        // the enmity owners still worth hating: a char logged in, in the mob's zone and up; a char's pet or trust up in
        // the mob's zone; another mob (a charmed one, a dynamic entity ally of the players such as the Garrison's, a mob
        // of another allegiance) spawned and up in the mob's zone: CEnmityContainer::GetHighestEnmity keeps any owner in
        // the holder's zone and instance that is not isDead (no HP, Disappear, the death or despawn state)
        private bool EnmityOwnerValid(MobEntity m, BattleEntity e) => e switch
        {
            CharData c => CharById(c.Id).Item1 == c && SameArea(c, m) && c.Hp > 0,
            PetEntity p => SameArea(p, m) && p.Hp > 0 && !PetIsDead(p) && AreaOf(p).Pets.Contains(p),
            TrustEntity t => SameArea(t, m) && t.Hp > 0 && t.DeathAtMs == 0 && t.PMaster != null && AreaOf(t).Trusts.Contains(t),
            MobEntity other => SameArea(other, m) && other.IsSpawned && other.IsAlive && !other.IsDead,
            _ => false,
        };

        private BattleEntity MobHighestEnmity(MobEntity m) => m.Enmity.GetHighestEnmity(e => EnmityOwnerValid(m, e), EnmityCharById);

        // zoneutils::GetChar for an enmity entry a char's zone-out left without its owner (LogoutReset)
        private BattleEntity EnmityCharById(uint id) => CharById(id).Item1;

        // CAIContainer::Tick for a living mob, every logic tick: the controller, then the attack state
        private void TickMobAi(MobEntity m, long now)
        {
            if (!m.IsAlive || m.IsDead || now < m.NextAiTickMs) return;
            m.NextAiTickMs = now + MobAiTickMs;
            // a charmed mob runs CPetController (ZoneHost.Charm.cs)
            if (IsCharmedPet(m))
            {
                TickCharmedMob(m, now);
                return;
            }
            if (m.Engaged) MobCombatTick(m, now);
            // DoRoamTick: an idle buff (DoBuffTick) is cast before any roaming
            else if (m.SkillState == null && !(m.SpellContainer.HasSpells && !(m.PathFind?.IsPatrolling ?? false) && DoBuffTick(m, now)))
                MobRoamTick(m, now);
            // the state on top: a mob skill, a spell, a ranged attack or being inactive holds the attack state's swing
            // until it is done (the inactive kind that may not change state interrupted the first three)
            if (m.SkillState != null) TickMobSkill(m, now);
            else if (m.MagicState != null) TickMagic(m, now);
            else if (m.RangedState != null) TickMobRanged(m, now);
            else if (TickMobAbility(m, now)) { }
            else if (TickInactive(m, now)) { }
            else if (m.Engaged) UpdateMobAttackState(m, now);
            CheckGoInactive(m, now);
        }

        // DoRoamTick: anyone on the enmity list, then the claimer, pull the mob into combat
        private void MobRoamTick(MobEntity m, long now)
        {
            bool ignoreAggro = (m.RoamFlags & RoamFlag.Ignore) != 0;
            if (!ignoreAggro && MobHighestEnmity(m) is { } highest)
            {
                MobEngage(m, highest, now);
                return;
            }
            if (m.OwnerId != 0 && !ignoreAggro)
            {
                if (CharById(m.OwnerId).Item1 is { } owner && EnmityOwnerValid(m, owner)) MobEngage(m, owner, now);
                return;
            }
            if (ignoreAggro) m.OwnerId = 0;

            // an idle mob with no walkable ground anywhere near it despawns 200 ms on; a pathing one is exempt (a
            // waypoint can read off-mesh on a poly seam), as is a NO_DESPAWN one
            var path = PathOf(m);
            if (!path.IsFollowingPath && !path.ValidPosition(m.X, m.Y, m.Z, now))
            {
                bool noDespawn = m.GetMobMod(MobMod.NoDespawn) != 0 || ChatCfg.MobNoDespawn;
                if (!noDespawn && m.DespawnAtMs == 0 && !path.NearValidPosition(m.X, m.Y, m.Z)) m.DespawnAtMs = now + 200;
                return;
            }
            // a roam follower stays by its leader
            if (MobRoamFollow(m, now)) return;

            // (the C++ skips the rest while nothing is due and the mob is unmoved: here it runs every tick, so neutral
            // can end a few seconds sooner)
            if (now >= m.MobHealAtMs + MobRestIntervalMs && m.GetMobMod(MobMod.NoRest) == 0 && m.GetMod(Mod.REGEN_DOWN) == 0)
            {
                if (MobRest(m, 0.1f)) m.UpdateMask |= EntityUpdatePacket.UpdateHp;
                // at full HP the EXP tracking starts fresh
                if (m.Hpp == 100)
                {
                    m.HiPcLvl = 0;
                    m.HiPartySize = 0;
                    m.GiveExp = true;
                    m.UsedSkillIds.Clear();
                }
                m.MobHealAtMs = now;
            }
            // ---- mob roaming (ZoneHost.MobRoam.cs): the wait, leaving neutral, walking the path or patrol, the
            // idle action (walk home, despawn when far, worm dive, scripted roam, RoamAround) and the 3 s ROAM_TICK /
            // onMobRoam. The idle action tries the special skill and the SMN summon first ----
            MobRoamAction(m, now);
            // ---- end mob roaming ----
        }

        // CBattleEntity::Rest: HP and MP back by the rate, TP down by 500 times it
        private static bool MobRest(MobEntity m, float rate)
        {
            bool didRest = false;
            if (m.Hp != m.MaxHp || m.Mp != m.MaxMp)
            {
                m.Hp = Math.Min(m.MaxHp, m.Hp + (int)(m.MaxHp * rate));
                m.Mp = Math.Min(m.MaxMp, m.Mp + (int)(m.MaxMp * rate));
                didRest = true;
            }
            if (m.Tp > 0)
            {
                m.Tp = Math.Clamp(m.Tp + (short)(rate * -500), 0, 3000);
                didRest = true;
            }
            return didRest;
        }

        // CMobController::Reset on spawn: a while before roaming, and neutral (no aggro) for a moment
        private void MobControllerReset(MobEntity m, long now)
        {
            int roamCool = m.GetMobMod(MobMod.RoamCool);
            m.LastActionMs = now - (roamCool > 0 ? MobRng.Next(roamCool) : 0) * 1000L;
            m.Neutral = true;
            m.NeutralAtMs = now;
            m.MobHealAtMs = 0;
            m.NextMagicMs = now + 1500;
            ClearFollowTarget(m);
        }

        // CZoneEntities::tapMobAggro: a mob the char has in range engages it when it can aggro (not too weak for the
        // char's level unless sitting or always aggressive) and notices it; a RoamFlag.Follow mob follows the char
        // instead when it could aggro it. A pet never does either.
        private void TapMobAggro(CharData c, MobEntity m)
        {
            if (!m.IsAlive || m.IsDead || c.Hp <= 0 || c.GmLevel >= 3 || m.PMaster != null) return;
            byte mobCheck = CheckMob(c.MLvl, m);
            if ((m.RoamFlags & RoamFlag.Follow) != 0)
            {
                if (CanFollowTarget(m, c)) SetFollowTarget(m, c, FollowRoam, NowMs());
                return;
            }
            bool validAggro = mobCheck > MobTooWeak || IsSitting(c) || m.GetMobMod(MobMod.AlwaysAggro) != 0;
            if (validAggro && CanAggroTarget(m, c)) MobEngage(m, c, NowMs());
        }

        // CBattleEntity::isSitting
        private static bool IsSitting(BattleEntity e) =>
            e.Animation == Anim.Healing || e.Animation == Anim.Sit || e.Animation >= Anim.Sitchair0 && e.Animation <= Anim.Sitchair10;

        // CMobController::CanAggroTarget: an aggressive mob, not neutral, not a CoP fomor the char has not angered
        // enough, not a worm underground, not a Monipulator that preys on it, at a target of its battle id up and on
        // foot it detects
        private bool CanAggroTarget(MobEntity m, CharData c)
        {
            if (m.BattleId != c.BattleId) return false;
            if (m.GetMobMod(MobMod.AlwaysAggro) == 0 && !m.Aggro || m.Neutral || !m.IsAlive) return false;
            if (m.GetMobMod(MobMod.NoAggro) > 0) return false;
            bool isCopFomorZone = m.Family == FamilyFomor && (m.Type & MobType.Notorious) == 0 &&
                                  m.Zone >= ZoneLufaiseMeadows && m.Zone <= ZoneSacrarium;
            if (isCopFomorZone && CharVar(c, "FOMOR_HATE") < 8) return false;
            if ((m.RoamFlags & RoamFlag.Worm) != 0 && (m.NameVis & MobEntity.NameVisHideName) != 0) return false;
            // Players only: a monster ignores a Monipulator that preys on it.
            if (m.Ecosystem != Ecosystem.Unclassified && EcosystemStrongAgainst(c.EcoSystem) == m.Ecosystem) return false;
            if (c.Hp <= 0 || c.StatusEffects.HasStatusEffect(EffectId.Mounted)) return false;
            return m.PMaster == null && m.IsSpawned && !m.Engaged && CanDetectTarget(m, c);
        }

        private byte[] ecosystemStrongAgainst_;

        // battleutils::GetEcosystemStrongAgainst: the ecosystem this one preys on, column 1 (STRONG_AGAINST) of
        // xi.data.entityCorrelation.dataTable, cached once the scripts are up; Unclassified when it preys on nothing
        private byte EcosystemStrongAgainst(byte ecosystem)
        {
            if (ecosystemStrongAgainst_ == null)
            {
                if (lua_ == null) return Ecosystem.Unclassified;
                var table = new byte[256];
                int top = lua_.Top;
                try
                {
                    lua_.GetPath("xi.data.entityCorrelation.dataTable");
                    if (!lua_.IsTable(-1)) log_("entityCorrelation.dataTable is missing. Ecosystem correlation is disabled.");
                    else
                    {
                        int t = lua_.Top;
                        lua_.PushNil();
                        while (lua_.Next(t))
                        {
                            // Column 1 of the row is STRONG_AGAINST, and is nil for anything with no correlation.
                            if (lua_.IsNumber(-2) && lua_.IsTable(-1))
                            {
                                // key.as<uint32>() and get<sol::optional<uint32>>(1): sol2's llround
                                uint index = (uint)lua_.ToIntegral(-2);
                                lua_.RawGetI(-1, 1);
                                if (index < table.Length && lua_.IsNumber(-1))
                                {
                                    uint preysOn = (uint)lua_.ToIntegral(-1);
                                    if (preysOn < table.Length) table[index] = (byte)preysOn;
                                }
                                lua_.Pop(1);
                            }
                            lua_.Pop(1);
                        }
                    }
                }
                finally
                {
                    lua_.SetTop(top);
                }
                ecosystemStrongAgainst_ = table;
            }
            return ecosystemStrongAgainst_[ecosystem];
        }

        // CMobController::Engage and the CAttackState it starts: the first swing 3 s off, the weapon drawn
        private void MobEngage(MobEntity m, BattleEntity target, long now)
        {
            m.BattleTarget = target;
            if (m.Engaged) return;
            m.Engaged = true;
            m.BattleStartMs = now;
            if (m.FollowType == FollowRoam && FollowTargetOf(m) != null) ClearFollowTarget(m);
            m.TpThreshold = MobRng.Next(1000, 3001);
            // CMobController::Engage: the aggro spell first, after the magic delay
            m.FirstSpell = true;
            int magicDelay = m.GetMobMod(MobMod.MagicDelay);
            if (magicDelay != 0) m.NextMagicMs = Math.Max(m.NextMagicMs, now) + MobRng.Next(magicDelay) * 1000L;
            m.DeaggroTimeMs = now;
            m.SwingDelayMs = MeleeFor().WeaponDelay(m, false);
            m.SwingAtMs = now + MobEngageDelayMs;
            m.DespawnAtMs = 0;
            m.Animation = Anim.Attack;
            m.UpdateMask |= EntityUpdatePacket.UpdateHp;
            ResetMobChase(m);
            // CBattleEntity::OnEngage's ENGAGE listeners, then CMobEntity::OnEngage's script
            TriggerListener(m, "ENGAGE", m, target);
            OnMobEngage(m, target);
            EnmityAlliHate(m, target);
            // CMobController::Engage: the special skill's opening delay, then the pet engages the same target
            if (m.GetMobMod(MobMod.SpecialDelay) != 0)
                m.LastSpecialMs = now - (m.GetMobMod(MobMod.SpecialCool) + MobRng.Next(m.GetMobMod(MobMod.SpecialDelay))) * 1000L;
            if (m.PPet is MobEntity pet && pet.IsSpawned && pet.IsAlive && !pet.Engaged) MobEngage(pet, target, now);
        }

        // CMobController::Disengage with CMobEntity::OnDisengage: the list and the claim go, the weapon is put away
        private void MobDisengage(MobEntity m, long now)
        {
            m.Engaged = false;
            MobControllerDisengage(m, now);
            if (m is AutomatonEntity automaton) AutomatonDisengaged(automaton);
            TriggerListener(m, "DISENGAGE", m);
            OnMobDisengage(m);
        }

        // CMobController::Disengage alone (the disengage binding on a mob not attacking: no attack state ends, so no
        // OnDisengage)
        private void MobControllerDisengage(MobEntity m, long now)
        {
            m.BattleTarget = null;
            ResetMobChase(m);
            m.PathFind?.Clear();
            m.Enmity.Clear();
            if (m.GetMobMod(MobMod.IdleDespawn) != 0) m.DespawnAtMs = now + m.GetMobMod(MobMod.IdleDespawn) * 1000L;
            if (m.OwnerId != 0 && CharById(m.OwnerId).Item1 is { } owner && owner.ClaimedMob == m) owner.ClaimedMob = null;
            m.OwnerId = 0;
            m.EntityFlags &= ~MobEntity.EntityFlagCallForHelp;
            m.Animation = Anim.None;
            m.ThLvl = m.GilfinderLevel = 0; // assumed to work like TH
            m.UpdateMask |= EntityUpdatePacket.UpdateStatus | EntityUpdatePacket.UpdateHp;
            // CMobController::Disengage: neutral for a while, a roam decision (walking home) 10 s on, the rest 10 s off
            m.LastActionMs = now - m.GetMobMod(MobMod.RoamCool) * 1000L + 10000;
            m.Neutral = true;
            m.NeutralAtMs = now;
            m.PathFind?.Clear();
            m.MobHealAtMs = now;
        }

        // DoCombatTick
        private void MobCombatTick(MobEntity m, long now)
        {
            // HandleEnmity
            m.Enmity.DecayEnmity();
            if (MobHighestEnmity(m) is { } highest) m.BattleTarget = highest;
            FaceTarget(m, m.BattleTarget);
            if (TryDeaggro(m, now))
            {
                AbortMagic(m, now);
                MobDisengage(m, now);
                return;
            }
            MobTryLink(m, now);
            TriggerListener(m, "COMBAT_TICK", m);
            OnMobFight(m, m.BattleTarget);
            // a script may have let the target go, or killed the mob
            if (!m.Engaged || m.BattleTarget == null || m.InactiveState != null || !CanChangeState(m)) return;
            if (MobRunAwayFollow(m, now)) return;
            // the special skill first
            if (IsSpecialSkillReady(m, Distance(m, m.BattleTarget), now) && TrySpecialSkill(m, now)) return;
            // a spell, when the magic cool is up
            float meleeRange = m.BattleTarget != null ? MeleeRange(m, m.BattleTarget) : 0;
            if (IsSpellReady(m, Distance(m, m.BattleTarget), meleeRange, now) && TryCastSpell(m, now)) return;
            // the TP move, once the TP passes the threshold (rolled again after each)
            if (now >= m.LastMobSkillMs && ShouldUseTpMove(m) && MobSkillFromList(m, 0))
            {
                m.TpThreshold = MobRng.Next(1000, 3001);
                return;
            }
            // a ranged attack, when enabled and the last one's free phase is over
            if (MobTryRangedAttack(m, now)) return;
            // Move: a scripted path (pathTo / pathThrough with SCRIPT) is walked instead of the chase
            if (!MobFollowScriptedPath(m, now)) MobMove(m, m.BattleTarget, now);
        }

        // TryDeaggro: a target that is gone, down, mounted, in another confrontation, on the mob's side, lost for 25 s,
        // hidden from it (CheckHide), locked (CheckLock) or of another battle id comes off the list, and the next
        // highest takes its place; with nobody left the mob disengages
        private bool TryDeaggro(MobEntity m, long now)
        {
            while (true)
            {
                var target = m.BattleTarget;
                if (target == null && MobHighestEnmity(m) == null) return true;
                bool lost = target == null || !EnmityOwnerValid(m, target) ||
                            target.StatusEffects.HasStatusEffect(EffectId.Mounted) ||
                            m.StatusEffects.GetConfrontationEffect() != target.StatusEffects.GetConfrontationEffect() ||
                            EnmityContainer.AllegianceOf(m) == EnmityContainer.AllegianceOf(target) || CheckDetection(m, target, now) ||
                            CheckHide(m, target) || CheckLock(target) || m.BattleId != target.BattleId;
                if (!lost) return false;
                if (target != null) m.Enmity.Clear(target.Id);
                var next = MobHighestEnmity(m);
                m.BattleTarget = next;
                if (next == null) return true;
                m.DeaggroTimeMs = now;
            }
        }

        // CheckDetection: noticing the target keeps the mob on it; a mob that can deaggro (not an NM or battlefield
        // mob) lets go 25 s after it last did
        private bool CheckDetection(MobEntity m, BattleEntity target, long now)
        {
            bool isImmobilised = m.StatusEffects.HasStatusEffect(EffectId.Bind) || m.StatusEffects.HasStatusEffect(EffectId.SleepI) ||
                                 m.StatusEffects.HasStatusEffect(EffectId.SleepII) || m.StatusEffects.HasStatusEffect(EffectId.Lullaby) ||
                                 m.StatusEffects.HasStatusEffect(EffectId.Petrification);
            if (CanTrackByScent(m, target) || CanDetectTarget(m, target) || isImmobilised) m.DeaggroTimeMs = now;
            bool canDeaggro = (m.Type & (MobType.Notorious | MobType.Battlefield)) == 0;
            return canDeaggro && now >= m.DeaggroTimeMs + MobDeaggroMs + ChatCfg.MobAdditionalTimeToDeaggro * 1000L;
        }

        // CMobController::CheckHide: a THF under Hide is lost to a mob that can't track it by scent, has no true
        // detection and doesn't detect by hearing
        private bool CheckHide(MobEntity m, BattleEntity target) =>
            target != null && target.MJob == MobStats.JobThf && target.StatusEffects.HasStatusEffect(EffectId.Hide) &&
            !CanTrackByScent(m, target) && !m.TrueDetection && (m.GetMobMod(MobMod.Detection) & DetectsHearing) == 0;

        // CMobController::CheckLock: the char (or a pet's master) is locked in place (m_Locked)
        private static bool CheckLock(BattleEntity target) => target switch
        {
            CharData c => c.Locked,
            PetEntity { PMaster: CharData master } => master.Locked,
            _ => false,
        };

        // CMobController::CanTrackByScent: a mob that tracks by scent, out of the water, at a target without Deodorize,
        // unless the weather has washed the scent out (DisableScent)
        private bool CanTrackByScent(MobEntity m, BattleEntity target)
        {
            if ((m.GetMobMod(MobMod.Detection) & DetectsScent) == 0) return false;
            // mobs underwater instantly deaggro if scent fails
            if (InWater(m) || target == null || target.StatusEffects.HasStatusEffect(EffectId.Deodorize)) return false;
            return !m.DisableScent;
        }

        // CanDetectTarget: sight in front, an ambush up close, hearing, a spell with an MP cost being cast nearby, or
        // within 20 yalms a target under 75% HP or mid weapon skill / ability; sight, hearing, magic, low HP and
        // abilities need a line of sight unless the target is already the mob's, in melee reach
        private bool CanDetectTarget(MobEntity m, BattleEntity target)
        {
            if (target.Hp <= 0 || target.StatusEffects.HasStatusEffect(EffectId.Mounted)) return false;
            if (target.StatusEffects.HasStatusEffect(EffectId.Gestation)) return false;
            int detects = m.GetMobMod(MobMod.Detection);
            float dist = Distance(m, target);
            bool invisible = false, sneak = false;
            if (!m.TrueDetection)
            {
                invisible = target.StatusEffects.HasStatusEffectByFlag(EffectFlag.Invisible);
                sneak = target.StatusEffects.HasStatusEffect(EffectId.Sneak);
            }
            if (target.StatusEffects.HasStatusEffect(EffectId.Illusion) && m.GetMobMod(MobMod.SeesThroughIllusion) == 0)
                invisible = sneak = true;
            bool isTargetAndInRange = m.BattleTarget == target && dist <= MeleeRange(m, target);
            bool Detected() => isTargetAndInRange || CanSeeTarget(m, target);
            if ((detects & DetectsSight) != 0 && !invisible && dist < m.GetMobMod(MobMod.SightRange) && Facing(m, target)) return Detected();
            if ((m.Behavior & BehaviorAggroAmbush) != 0 && dist < 3 && !sneak) return true;
            if ((detects & DetectsHearing) != 0 && dist < m.GetMobMod(MobMod.SoundRange) && !sneak) return Detected();
            if ((detects & DetectsMagic) != 0 && dist < m.GetMobMod(MobMod.MagicRange) && target.MagicState?.Spell is { HasMpCost: true }) return Detected();
            // the remaining detection types only fire at close range
            if (dist > 20) return false;
            if ((detects & DetectsLowHp) != 0 && target.Hpp < 75) return Detected();
            if ((detects & DetectsAbility) != 0 && InWeaponSkillOrAbility(target)) return Detected();
            return false;
        }

        // PAI->IsCurrentState<CWeaponSkillState>() || IsCurrentState<CAbilityState>()
        private static bool InWeaponSkillOrAbility(BattleEntity b) => b switch
        {
            CharData c => c.WsState != null || c.AbilityState != null,
            TrustEntity t => t.WsState != null || t.AbilityState != null,
            MobEntity mb => mb.AbilityState != null,
            _ => false,
        };

        // CMobController::CanSeeTargetCached: the mob's line of sight to its battle target, worked out once a tick
        private bool MobCanSeeTargetCached(MobEntity m, BattleEntity target, long now)
        {
            if (target == null) return false;
            if (m.LosTickAt == now && m.LosTickTarget == target) return m.LosTickResult;
            m.LosTickAt = now;
            m.LosTickTarget = target;
            m.LosTickResult = CanSeeTarget(m, target);
            return m.LosTickResult;
        }

        // CMobController::FaceTarget (PathFind::LookAt)
        private static void FaceTarget(MobEntity m, BattleEntity target)
        {
            if (target == null || (m.Behavior & BehaviorNoTurn) != 0) return;
            byte rot = WorldAngle(m.X, m.Z, m.Rotation, target.X, target.Z);
            if (rot == m.Rotation) return;
            m.Rotation = rot;
            m.UpdateMask |= EntityUpdatePacket.UpdatePos;
        }

        // CBattleEntity::GetMeleeRange
        private static float MeleeRange(MobEntity m, BattleEntity target) => m.ModelHitboxSize + 2.0f + HitboxOf(target);

        // modelHitboxSize: a char's in tenths, a mob's in yalms
        private static float HitboxOf(BattleEntity e) => e switch
        {
            CharData c => c.ModelHitboxSize10 / 10.0f,
            MobEntity mob => mob.ModelHitboxSize,
            _ => 0,
        };

        private const float MobChaseRepathDrift = 2.0f;         // kChaseRepathDrift: re-aim once the target drifts this far
        private const long MobRePathCooldownMs = 2000;          // kRePathCooldown
        private const long MobLostSightRePathCooldownMs = 250;  // kLostSightRePathCooldown

        // the chase state Engage and Disengage reset
        private static void ResetMobChase(MobEntity m)
        {
            m.RePathCooldownEndMs = long.MinValue / 2;
            m.LastRePathTarget = default;
            m.LastRePathMobPos = default;
            m.StuckRePathCount = 0;
            m.LastDirectProbeTarget = null;
            m.LastDirectProbePos = default;
            m.LastDirectProbeTargetPos = default;
            m.LastDirectProbeWasDirect = true;
        }

        private static bool WithinDistance((float X, float Y, float Z) a, float bx, float by, float bz, float within)
        {
            float dx = a.X - bx, dy = a.Y - by, dz = a.Z - bz;
            return dx * dx + dy * dy + dz * dz <= within * within;
        }

        // CMobController::Move: the attack range (the ranged one, else the attack skill list's first move's, else melee)
        // and the close distance 0.4 inside it; a SHARE_POS mob mirrors its leader. In range the mob settles, unless
        // it must stand back or the navmesh route there (probed when the target or either side moved) is a detour
        // worth walking; out of range it re-paths when the target drifted 2 yalms from where the path heads, or
        // every 2 s, a mob that went nowhere twice warping in beside the target (unless NO_STUCK_TELEPORT); each
        // step stops at the close distance, and once there a mob stacked on another of the char's steps aside.
        // TELEPORT_TYPE 1 jumps in (the TELEPORT_START skill on itself) from out of reach within 30 yalms every
        // TELEPORT_CD seconds; TELEPORT_TYPE 2 only ever warps, within the skill's distance, and never walks.
        private void MobMove(MobEntity m, BattleEntity target, long now)
        {
            var path = PathOf(m);

            float attackRange = m.RangedAttackEnabled ? MobRangedAttackRange(m) : target != null ? MeleeRange(m, target) : 0;
            if (!m.RangedAttackEnabled && m.GetMobMod(MobMod.AttackSkillList) > 0 &&
                GetMobSkillList(m.GetMobMod(MobMod.AttackSkillList)) is { Count: > 0 } skillList && GetMobSkill(skillList[0]) is { } attackSkill)
                attackRange = attackSkill.Distance;
            int offsetMod = m.GetMobMod(MobMod.TargetDistanceOffset);
            float closeDistance = Math.Max(0.0f, attackRange - (offsetMod == 0 ? 0.4f : offsetMod / 10.0f));

            if (m.GetMobMod(MobMod.SharePos) > 0)
            {
                if (AreaOf(m).MobById.TryGetValue(m.Id + (uint)m.GetMobMod(MobMod.SharePos), out var leader))
                    (m.X, m.Y, m.Z, m.Rotation) = (leader.X, leader.Y, leader.Z, leader.Rotation);
                else log_("CMobController::Move() failed to get mob for xi::MobMod::SharePos");
                return;
            }
            if (target == null) return;

            float currentDistance = Distance(m, target);
            bool isFollowingPath = path.IsFollowingPath;

            if (m.GetMobMod(MobMod.TeleportType) == 1 && currentDistance > attackRange && currentDistance <= 30.0f &&
                now >= m.LastSpecialMs + m.GetMobMod(MobMod.TeleportCd) * 1000L &&
                GetMobSkill((ushort)m.GetMobMod(MobMod.TeleportStart)) is { } jumpIn)
            {
                m.LastSpecialMs = now;
                MobUseSkill(m, m, jumpIn.Id, null, now);
            }

            // in range it faces the target, but keeps checking the line of sight so it can't attack through a thin wall
            bool inAttackRange = currentDistance <= attackRange;
            if (!MobCanFollowPath(m))
            {
                FaceTarget(m, target);
                return;
            }

            if (inAttackRange && MobCanSeeTargetCached(m, target, now))
            {
                bool canMove = m.Speed != 0 && m.GetMobMod(MobMod.NoMove) == 0 && now >= m.LastSpecialMs;
                if (!canMove || !MobShouldCloseToTarget(m, target, currentDistance, now))
                {
                    FaceTarget(m, target);
                    return;
                }
                bool targetChanged = m.LastDirectProbeTarget != target;
                bool mobMoved = !WithinDistance(m.LastDirectProbePos, m.X, m.Y, m.Z, 1.0f);
                bool targetMovedSinceProbe = !WithinDistance(m.LastDirectProbeTargetPos, target.X, target.Y, target.Z, 1.0f);
                if (targetChanged || mobMoved || targetMovedSinceProbe)
                {
                    m.LastDirectProbeTarget = target;
                    m.LastDirectProbePos = (m.X, m.Y, m.Z);
                    m.LastDirectProbeTargetPos = (target.X, target.Y, target.Z);
                    path.PathInRange(target.X, target.Y, target.Z, closeDistance, PathFlag.Run);
                    // tighter than 2.0 so corner detours aren't treated as direct
                    m.LastDirectProbeWasDirect = path.IsPathDirect(1.1f);
                    if (m.LastDirectProbeWasDirect) path.Clear();
                }
                if (m.LastDirectProbeWasDirect)
                {
                    FaceTarget(m, target);
                    return;
                }
            }

            if (m.Speed == 0 || m.GetMobMod(MobMod.NoMove) != 0 || now < m.LastSpecialMs) return;

            if (m.GetMobMod(MobMod.TeleportType) == 2)
            {
                if (GetMobSkill((ushort)m.GetMobMod(MobMod.TeleportStart)) is { } warp && currentDistance <= warp.Distance)
                {
                    MobUseSkill(m, m, warp.Id, null, now);
                    m.LastSpecialMs = now;
                }
                return;
            }

            if (!MobShouldCloseToTarget(m, target, currentDistance, now))
            {
                FaceTarget(m, target);
                return;
            }

            bool needNewPath, isStuckRepath = false, targetMoved = false;
            // isFollowingPath is from before the in-range probe, whose PathInRange clears the path and may find none:
            // the C++ then reads GetDestination off an empty path (points_.back(), undefined); an emptied path counts
            // as not followed here
            if (isFollowingPath && path.IsFollowingPath) needNewPath = !WithinDistance(path.Destination, target.X, target.Y, target.Z, MobChaseRepathDrift);
            else
            {
                bool outOfRange = currentDistance > attackRange;
                bool lostLos = !MobCanSeeTargetCached(m, target, now);
                bool cooldownDone = now >= m.RePathCooldownEndMs;
                bool losCooldownDone = now >= m.LostSightRePathCooldownEndMs;
                targetMoved = !WithinDistance(m.LastRePathTarget, target.X, target.Y, target.Z, MobChaseRepathDrift);
                // lost sight has its own short leash, so the mob keeps trying without hammering the pathfinder
                needNewPath = outOfRange && (targetMoved || cooldownDone) || lostLos && (targetMoved || losCooldownDone);
                // stuck means the mob went nowhere on its last path
                bool mobMoved = !WithinDistance(m.LastRePathMobPos, m.X, m.Y, m.Z, 1.0f);
                isStuckRepath = outOfRange && cooldownDone && !targetMoved && !mobMoved;
            }

            if (needNewPath)
            {
                m.LastRePathTarget = (target.X, target.Y, target.Z);
                m.LastRePathMobPos = (m.X, m.Y, m.Z);
                m.RePathCooldownEndMs = now + MobRePathCooldownMs;
                m.LostSightRePathCooldownEndMs = now + MobLostSightRePathCooldownMs;
                if (isStuckRepath) ++m.StuckRePathCount;
                else if (targetMoved) m.StuckRePathCount = 0;

                if (m.StuckRePathCount >= 2 && m.GetMobMod(MobMod.NoStuckTeleport) == 0)
                {
                    path.WarpTo(target.X, target.Y, target.Z, target.Rotation, closeDistance);
                    m.StuckRePathCount = 0;
                    m.RePathCooldownEndMs = long.MinValue / 2;
                }
                else path.PathTo(target.X, target.Y, target.Z, PathFlag.Run); // nearPosition(target, 0, ...): the target's spot
            }

            // out of range a step stops at the close distance from where the target is now; in range the mob walks a
            // detour and takes the full step
            float stepCap = inAttackRange ? float.MaxValue : currentDistance - closeDistance;
            path.FollowPath(now, stepCap);
            if (path.IsFollowingPath) return;

            // arrived: shuffle aside if another of the char's mobs is stacked on this one
            if (target is CharData c)
            {
                foreach (var other in AreaOf(m).Mobs)
                {
                    if (other == m || !c.SpawnMobList.Contains(other.Id) || other.PathFind is { IsFollowingPath: true } || Distance(other, m) >= 1.0f) continue;
                    var (sx, sy, sz) = SidestepPosition(m, target, 1.5f);
                    if (path.ValidPosition(sx, sy, sz, now))
                    {
                        path.PathTo(sx, sy, sz, PathFlag.Run);
                        return;
                    }
                    break;
                }
            }
            FaceTarget(m, target);
        }

        // ShouldCloseToTarget: inside the standback range (20 unless STANDBACK_RANGE; not while closing to ranged
        // range) a STANDBACK mob holds, and a healthy caster (HP_STANDBACK, not NO_STANDBACK, its HP and MP at the
        // threshold) holds while it has something to cast (a NIN always closes); a mob that has lost sight of the
        // target closes in regardless of its leash; past SPAWN_LEASH from home it holds. The STANDBACK hold needs a
        // line of sight.
        private bool MobShouldCloseToTarget(MobEntity m, BattleEntity target, float currentDistance, long now)
        {
            int standbackRangeMod = m.GetMobMod(MobMod.StandbackRange);
            int standbackHpThreshold = m.GetMobMod(MobMod.HpStandback);
            int standbackRange = standbackRangeMod > 0 ? standbackRangeMod : 20;
            bool isClosingToRangedAttackRange = m.RangedAttackEnabled && currentDistance > MobRangedAttackRange(m);
            bool isInsideStandbackRange = !isClosingToRangedAttackRange && currentDistance < standbackRange;
            if (isInsideStandbackRange && (m.Behavior & BehaviorStandback) != 0 && MobCanSeeTargetCached(m, target, now)) return false;
            bool wantsHealthStandback = isInsideStandbackRange && standbackHpThreshold > 0 && m.GetMobMod(MobMod.NoStandback) == 0 &&
                                        m.Hpp >= standbackHpThreshold && (m.MaxMp == 0 || m.Mpp >= standbackHpThreshold);
            if (wantsHealthStandback)
                return m.MJob != MobStats.JobNin && m.SpellContainer.HasSpells && !CanCastSpells(m, true);
            if (target != null && !MobCanSeeTargetCached(m, target, now)) return true;
            if (m.GetMobMod(MobMod.SpawnLeash) > 0 && MobDistanceFromHome(m) > m.GetMobMod(MobMod.SpawnLeash)) return false;
            return true;
        }

        // pathfind::stepTowards: face the point, then one step along the facing, settling stopShort away when that is
        // within reach; the height follows the slope
        private static void StepTowards(MobEntity m, float x, float y, float z, float step, float stopShort)
        {
            float dx = x - m.X, dz = z - m.Z, dy = y - m.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            if (dx * dx + dz * dz > 0.1f * 0.1f) m.Rotation = WorldAngle(m.X, m.Z, m.Rotation, x, z);
            float radians = 2.0f * MathF.PI - m.Rotation * 2.0f * MathF.PI / 256.0f;
            bool settle = dist <= stopShort + step;
            float advance = settle ? Math.Max(dist - stopShort, 0.0f) : step;
            if (settle && stopShort == 0.0f) (m.X, m.Y, m.Z) = (x, y, z);
            else
            {
                m.X += MathF.Cos(radians) * advance;
                m.Z += MathF.Sin(radians) * advance;
                float flat = MathF.Sqrt(dx * dx + dz * dz);
                m.Y += flat > 0.0f ? dy * Math.Min(advance / flat, 1.0f) : dy;
            }
            m.Moving = (ushort)((m.Moving + 0x35) % 0x2000);
            m.UpdateMask |= EntityUpdatePacket.UpdatePos;
        }

        // CAttackState::Update for a mob: a swing whenever one is due and the target is in reach
        private void UpdateMobAttackState(MobEntity m, long now)
        {
            if (m.BattleTarget is not BattleEntity target || target.Hp <= 0 || now < m.SwingAtMs) return;
            // CanAttack: not while the navmesh route is far longer than the straight line, then in reach, with
            // auto-attack on (setAutoAttackEnabled), and never without a line of sight (thin geometry). An
            // ATTACK_SKILL_LIST mob's reach is its list's first skill's distance between the two hitboxes
            // (CMobEntity::CanAttack's canAttackInRange), anyone else's the melee range
            bool detouring = m.PathFind is { IsFollowingPath: true } detour && !detour.IsPathDirect();
            int attackSkillList = m.GetMobMod(MobMod.AttackSkillList);
            float reach = MeleeRange(m, target);
            if (attackSkillList != 0 && GetMobSkillList(attackSkillList) is { Count: > 0 } skillList && GetMobSkill(skillList[0]) is { } firstSkill)
                reach = m.ModelHitboxSize + firstSkill.Distance + HitboxOf(target);
            if (!detouring && Distance(m, target) <= reach && m.AutoAttackEnabled && CanSeeTarget(m, target))
            {
                m.SwingAtMs += m.SwingDelayMs;
                m.DeaggroTimeMs = now; // CMobEntity::OnAttack's TapDeaggroTime
                if (attackSkillList != 0)
                {
                    // CMobEntity::OnAttack: the round is a skill of the list (CMobController::MobSkill(listId)), its
                    // own action; no BATTLE2 of the attack state's
                    if (MobSkillFromList(m, attackSkillList)) EnmityHandleKillshot(m, target);
                }
                else
                {
                    var action = MobAttack(m, target, now);
                    EnmityHandleKillshot(m, target);
                    if (action != null) PushMobInRange(m, Battle2Packets.Battle2(action));
                }
                if (target is CharData tc && SessionOf(tc) is { } s) SendUpdateMask(s, tc);
            }
            if (m.SwingAtMs < now) m.SwingAtMs = now; // no banking time while it cannot swing
        }

        // CHAR_INRANGE for a mob (or a pet, or a trust): the chars that have it spawned
        private void PushMobInRange(MobEntity m, BasicPacket p)
        {
            foreach (var (c, s) in charSessions_)
                if (SameArea(c, m) && (m is PetEntity ? c.SpawnPetList : m is TrustEntity ? c.SpawnTrustList : c.SpawnMobList).Contains(m.Id))
                    s.PacketList.AddLast(new BasicPacket(p.Buffer));
        }

        // CBattleEntity::OnAttack with a mob (or a pet, or a trust) swinging: the claim on a mob target, then nothing
        // (null) when paralysis or intimidation stops the round
        private ActionPacket MobAttack(MobEntity m, BattleEntity target, long now)
        {
            if (target is MobEntity claimed) ClaimMob(claimed, m, now);
            if (target is TrustEntity attacked) attacked.LastAttackedMs = now;
            if (AttackInterrupted(m, target)) return null;
            var melee = MeleeFor();
            var action = new ActionPacket { ActorId = m.Id, ActionType = ActionCategory.BasicAttack };
            var t = action.AddTarget(target.Id);
            var swings = melee.BuildRound(m);
            while (swings.Count > 0 && target.Hp > 0 && m.IsAlive)
            {
                var swing = swings[0];
                var r = t.AddResult();
                r.Animation = Melee.Animation(swing);
                if (CheckCover(swing))
                {
                    target = swing.Round.CoverUser;
                    t.ActorId = target.Id;
                }
                MeleeSwing(m, target, r, swing, now);
                TryZanshin(m, swing, r, swings);
                swings.RemoveAt(0);
                if (t.Results.Count >= 8) break;
            }
            TriggerListener(m, "ATTACK", m, target, action);
            TriggerListener(target, "ATTACKED", target, m, action);
            lua_?.Forget(action);
            // Boost lasts the whole round
            m.StatusEffects.DelStatusEffectSilent(EffectId.Boost);
            m.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Detectable, EffectNotice.Silent);
            ProcessActionEffectFlags(m, action);
            action.Normalize();
            return action;
        }

        // battleutils::TakePhysicalDamage for a mob hitting a char: the char's HP (a block's share, a counter's always
        // hand-to-hand), its damage-broken effects, the mob's enmity toward it (a covered blow's: UpdateEnmityFromCover
        // between the original target and the cover user), the spell interrupt, and both sides' TP (none to a mob under
        // Meikyo Shisui, none to a counter's dealer)
        private int CharTakePhysicalDamage(BattleEntity attacker, CharData defender, int damage, int slot, bool isBlocked = false,
            bool giveTpToAttacker = true, bool isCounter = false, bool isCovered = false, BattleEntity originalTarget = null,
            bool isZanshin = false)
        {
            giveTpToAttacker = giveTpToAttacker && !attacker.StatusEffects.HasStatusEffect(EffectMeikyoShisui);
            int dmg = MeleeFor().MeleeDamageTaken(attacker, defender, damage, slot, isBlocked, isCounter, tp => AddTp(defender, (short)tp),
                isCovered, mp => BattleAddMp(defender, mp), giveTpToAttacker);
            dmg = PhysicalTakeDamage(defender, attacker, dmg, slot, isCounter);
            if (dmg > 0)
            {
                defender.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Damage);
                BindBreakCheck(defender);
                // (a charmed char hitting its party: no enmity, a char's TP)
                if (attacker is MobEntity mobAttacker)
                {
                    if (isCovered) mobAttacker.Enmity.UpdateEnmityFromCover(originalTarget, defender);
                    else mobAttacker.Enmity.UpdateEnmityFromAttack(defender, dmg);
                }
                // a hit may break a cast, unless Shield Mastery took it on the shield
                if (!(isBlocked && CharStats.HasTrait(defender, TraitShieldMastery))) TryHitInterrupt(defender, attacker);
                if (giveTpToAttacker)
                {
                    if (attacker is CharData charAttacker) AddTp(charAttacker, (short)Melee.SingleMeleeHitTpReturn(attacker, isZanshin));
                    else AddMobTp((MobEntity)attacker, (short)Melee.SingleMeleeHitTpReturn(attacker, isZanshin));
                }
                AddTp(defender, (short)Melee.TpGainOnPhysicalDamage(attacker, defender, dmg, Melee.BaseDelay(attacker)));
            }
            return dmg;
        }

        // lastAttackerId_.resolve(): the mob, char, pet or trust of the char's area that hit it last
        private BattleEntity LastAttackerOf(CharData c)
        {
            uint id = c.LastAttackerId;
            if (id == 0) return null;
            var area = AreaOf(c);
            return (BattleEntity)area.Mobs.Find(m => m.Id == id)
                ?? (CharById(id).Item1 is CharData pc && SameArea(pc, c) ? pc : null)
                ?? (BattleEntity)area.Pets.Find(p => p.Id == id)
                ?? area.Trusts.Find(t => t.Id == id);
        }

        // CCharEntity::Die: the defeat message, the claim let go, the fight over, weakness off (counted for the next
        // raise), the death state and its timer, the raise menu's delay and the reraise mods, from
        // map.EXP_LOSS_LEVEL the EXP lost (less map.EXP_RETAIN and EXPERIENCE_RETAINED; none while charmed, and in a
        // battlefield only under its RULES_LOSE_EXP), the pet despawned, onPlayerDeath. The params player:die staged
        // (nextDeath_: expLoss, Mijin Gakure's mijin) are taken up here and carried by the death state.
        private void CharDie(CharData c, long now)
        {
            var killer = LastAttackerOf(c);
            PushCharInRange(c, killer != null
                ? SelfPackets.BattleMessage(killer.Id, killer.TargId, c.Id, c.TargId, 0, 0, MsgPlayerDefeatedBy)
                : SelfPackets.BattleMessage(c.Id, c.TargId, c.Id, c.TargId, 0, 0, MsgFallsToGround), true);
            RelinquishClaim(c, now);
            if (CharById(c.LastAttackerId).Item1 is CharData winner) MonstrosityBelligerentDefeat(c, winner);
            if (c.PPet != null) DespawnPet(c);
            var staged = c.NextDeath ?? DeathParams.Default;
            c.NextDeath = null;
            // Retail stopped charging EXP for a KO while charmed in June 2007
            var deathParams = new DeathParams(staged.LosesExp && !c.IsCharmed, staged.Mijin);
            CharEnterDeath(c, now, now, deathParams);
            c.BlockingAid = false;
            // a battlefield takes EXP only under its RULES_LOSE_EXP
            if (deathParams.LosesExp && loader_.Stats is { } stats && (c.PBattlefield == null || (c.PBattlefield.Rules & Battlefield.RulesLoseExp) != 0) &&
                c.MLvl >= stats.Settings.ExpLossLevel)
            {
                float retainPercent = Math.Clamp(stats.Settings.ExpRetain + c.GetMod(Mod.EXPERIENCE_RETAINED) / 100.0f, 0.0f, 1.0f);
                DelExperiencePoints(c, retainPercent, 0);
            }
            CallPlayerGlobal("xi.player.onPlayerDeath", c);
            if (SessionOf(c) is { } s)
            {
                c.UpdateMask |= CharStats.UpdateHp;
                SendUpdateMask(s, c);
            }
        }

        // CCharEntity::Die(duration): weakness off (counted for the next raise), the state stack cleared, the death
        // state (its raise menu's delay, the death-flagged effects gone, the death animation) with its timer running
        // from deathAtMs, the 16 s death sync, the reraise mods, then CBattleEntity::Die's DEATH and the battle target
        // dropped. The death state carries its params (DeathParams{} unless given).
        private void CharEnterDeath(CharData c, long now, long deathAtMs, DeathParams? deathParams = null)
        {
            ClearTrusts(c);
            if (c.StatusEffects.HasStatusEffect(EffectId.Weakness))
            {
                c.StatusEffects.DelStatusEffectSilent(EffectId.Weakness);
                c.WeaknessLvl++;
            }
            else
            {
                c.WeaknessLvl = 0;
            }
            c.DeathSyncAtMs = now + CharDeathSyncMs;
            // ClearStateStack: a cast is cleaned up (interrupted); the attack state goes without its cleanup
            AbortMagic(c, now);
            AbortMobSkill(c, now);
            c.InactiveState = null;
            c.AttackTarget = null;
            c.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Death, EffectNotice.Silent);
            c.Animation = Anim.Death;
            c.UpdateMask |= CharStats.UpdateHp;
            c.DeathAtMs = deathAtMs;
            c.DeathState = deathParams ?? DeathParams.Default;
            c.RaiseMenuAtMs = now + RaiseMenuDelayMs;
            c.RaiseSent = false;
            c.RaiseAcceptedAtMs = 0;
            ReraiseFromMods(c);
            c.History[HistoryTimesKnockedOut]++;
            TriggerListener(c, "DEATH", c);
            c.BattleTarget = null;
        }

        // the chars' deaths (CBattleEntity::PostTick) and their death states (CDeathState: the raise menu, an hour,
        // then home: OnDeathTimer forgets the EXP lost)
        private void TickCharDeaths(long now)
        {
            foreach (var c in new List<CharData>(charSessions_.Keys))
            {
                if (c.Hp <= 0 && c.DeathAtMs == 0) CharDie(c, now);
                else if (c.Hp > 0) c.DeathAtMs = 0;
                // CCharEntity::Tick: the dead char's status goes out every 16 s to keep the death counter in step
                else if (now >= c.DeathSyncAtMs && SessionOf(c) is { } ds)
                {
                    c.DeathSyncAtMs = now + CharDeathSyncMs;
                    c.UpdateMask |= CharStats.UpdateStatus;
                    SendUpdateMask(ds, c);
                }
                else if (now >= c.DeathAtMs + CharDeathDurationMs && c.RequestedWarp == WarpRequest.None)
                {
                    SetCharVar(c, "expLost", 0, 0);
                    // a Monipulator has no home point to go to (TODO in C++ too: what retail does when the menu times out)
                    if (c.Monstrosity != null) MonstrosityDeathMenu(c, DeathMenuMonstrosityCancel);
                    else c.RequestedWarp = WarpRequest.HomePoint;
                    continue;
                }
                TickRaise(c, now);
            }
        }
    }
}
