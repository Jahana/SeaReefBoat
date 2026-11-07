using System;
using System.Collections.Generic;

namespace XI.Zone
{
    // A mob's casting AI (M7b): CMobController's spell half (IsSpellReady, TryCastSpell with onMobSpellChoose and the
    // aggro / first spell, CastSpell's party buff target, DoBuffTick's idle buffs on itself and its link party, the
    // magic delay on engage, OnCastStopped's magic cool) and CMobSpellContainer's random pickers. The cast itself is
    // the shared CMagicState in ZoneHost.Magic.cs.
    public sealed partial class ZoneHost
    {
        // ---- the controller -----------------------------------------------------------------------------------------

        // CMobController::IsSpellReady: Chainspell / Manafont cast at will; a worm in melee range does not cast; away
        // from the target (beyond 5) the standback cool comes off the wait
        private bool IsSpellReady(MobEntity m, float currentDistance, float meleeRange, long now)
        {
            if (m.StatusEffects.HasStatusEffect(MagicFx.Chainspell, MagicFx.Manafont)) return true;
            bool worm = (m.RoamFlags & RoamFlag.Worm) != 0;
            if (worm && currentDistance <= meleeRange) return false;
            if (currentDistance > 5 && !worm) return now >= m.NextMagicMs - m.GetMobMod(MobMod.StandbackCool) * 1000L;
            return now >= m.NextMagicMs;
        }

        // CMobController::CanCastSpells
        private bool CanCastSpells(MobEntity m, bool ignoreRecastsAndCosts)
        {
            if (!m.SpellContainer.HasSpells) return false;
            if (m.StatusEffects.HasStatusEffect(EffectId.Silence, MagicFx.Mute)) return false;
            // SMN can only cast while pet-less
            if (m.MJob == MobStats.JobSmn && m.PPet != null && m.PPet.IsAlive) return false;
            if (!m.MagicCastingEnabled) return false;
            return ignoreRecastsAndCosts || IsAnySpellAvailable(m);
        }

        // CMobController::TryCastSpell: the engaged pick (the aggro spell first), onMobSpellChoose, then the cast if
        // the spell is off recast, affordable and the target in range
        private bool TryCastSpell(MobEntity m, long now)
        {
            if (!CanCastSpells(m, false)) return false;
            ushort? chosen;
            if (!m.Engaged) chosen = m.SpellContainer.HasBuffSpells() ? GetBuffSpell(m) : GetSpellToCast(m);
            else if (m.FirstSpell)
            {
                m.FirstSpell = false;
                chosen = GetAggroSpell(m);
            }
            else chosen = GetSpellToCast(m);

            var (spellOverride, targetOverride) = OnMobSpellChoose(m, m.BattleTarget ?? m, chosen);
            if (spellOverride is ushort o) chosen = o;
            if (chosen is not ushort id) return false;
            var spell = GetSpell(id);
            if (spell == null || RecastsOf(m).Has(RecastType.Magic, id) || !CanAffordSpell(m, spell, spell.Flag)) return false;

            BattleEntity castTarget = targetOverride ?? ((spell.ValidTargets & TargetSelf) != 0 ? m : m.BattleTarget);
            if (castTarget != null && Distance(m, castTarget) > spell.Range + HitboxSize(m) + HitboxSize(castTarget)) return false;
            if (targetOverride != null && castTarget != null) MobCast(m, castTarget, id, now);
            else MobCastSpell(m, id, now);
            return true;
        }

        // CMobController::TryCastIdleBuff: a buff for itself or a link party mate, through onMobSpellChoose
        private bool TryCastIdleBuff(MobEntity m, long now)
        {
            if (!CanCastSpells(m, false)) return false;
            if (PickIdleBuff(m) is not var (buffId, buffTarget)) return false;
            var (spellOverride, targetOverride) = OnMobSpellChoose(m, buffTarget, buffId);
            ushort id = spellOverride ?? buffId;
            var spell = GetSpell(id);
            if (spell == null || RecastsOf(m).Has(RecastType.Magic, id) || !CanAffordSpell(m, spell, spell.Flag)) return false;
            if (spellOverride != null && targetOverride == null)
            {
                MobCastSpell(m, id, now);
                return true;
            }
            var castTarget = targetOverride ?? buffTarget;
            if (Distance(m, castTarget) > spell.Range + HitboxSize(m) + HitboxSize(castTarget)) return false;
            MobCast(m, castTarget, id, now);
            return true;
        }

        // CMobController::DoBuffTick (out of combat): true while it casts or once it starts a buff
        private bool DoBuffTick(MobEntity m, long now)
        {
            if (m.MagicState != null) return true;
            if (m.PathFind?.IsFollowingPath == true) return false;
            if (!IsSpellReady(m, 0, 0, now) || !m.SpellContainer.HasBuffSpells()) return false;
            return TryCastIdleBuff(m, now);
        }

        // the idle buff and who gets it: usually itself, the nearer the lacking party mates the likelier them
        private (ushort, BattleEntity)? PickIdleBuff(MobEntity m)
        {
            (ushort, BattleEntity)? BuffFor(BattleEntity t)
            {
                if (t == null) return null;
                var spells = GetBuffSpellsFor(m, t);
                if (spells.Count == 0) return null;
                return (spells[MobRng.Next(spells.Count)], t);
            }
            var (nearest, lacking) = FindBuffAllies(m);
            if (MobRng.Next(1 + lacking) == 0) return BuffFor(m) ?? BuffFor(nearest);
            return BuffFor(nearest) ?? BuffFor(m);
        }

        // the link party mates (idle mobs, no master) close enough to buff that lack one of its buffs, and the nearest
        private (MobEntity, int) FindBuffAllies(MobEntity m)
        {
            if (m.LinkParty == null) return (null, 0);
            MobEntity nearest = null;
            float nearestSq = float.MaxValue;
            int lacking = 0;
            foreach (var c in m.LinkParty)
            {
                if (c == null || c == m || c.PMaster != null || !c.IsAlive || c.Engaged) continue;
                float range = 3.5f * (HitboxSize(m) + HitboxSize(c));
                float dx = c.X - m.X, dy = c.Y - m.Y, dz = c.Z - m.Z;
                float distSq = dx * dx + dy * dy + dz * dz;
                if (distSq > range * range || GetBuffSpellsFor(m, c).Count == 0) continue;
                lacking++;
                if (distSq < nearestSq)
                {
                    nearestSq = distSq;
                    nearest = c;
                }
            }
            return (nearest, lacking);
        }

        // CMobController::CastSpell: an enemy spell at the battle target; a party buff at a random mate, else itself
        private void MobCastSpell(MobEntity m, ushort id, long now)
        {
            var spell = GetSpell(id);
            if (spell == null)
            {
                log_($"ai_mob_dummy::CastSpell: SpellId <{id}> is not found");
                return;
            }
            BattleEntity target;
            if ((spell.ValidTargets & TargetSelf) == 0) target = m.BattleTarget;
            else if ((spell.ValidTargets & TargetPlayerParty) != 0) target = PickPartyBuffTarget(m, spell);
            else target = m;
            if (target != null) MobCast(m, target, id, now);
        }

        // pickPartyBuffTarget: half the time the master, else half the time itself, else a random valid party mate
        // fighting (or not) as it is
        private BattleEntity PickPartyBuffTarget(MobEntity m, Spell spell)
        {
            if (m.PMaster != null && MobRng.Next(2) == 0) return m.PMaster;
            if (MobRng.Next(2) != 0) return m;
            var candidates = new List<BattleEntity> { m };
            if (m.LinkParty != null)
                foreach (var c in m.LinkParty)
                {
                    if (c == null || c == m || !c.IsSpawned || !c.IsAlive || c.IsDead || !SameArea(c, m) || c.Allegiance != m.Allegiance) continue;
                    if (MathF.Abs(c.Y - m.Y) >= 8.5f || Distance(m, c) > spell.Range) continue;
                    candidates.Add(c);
                }
            var pick = candidates[MobRng.Next(candidates.Count)];
            return pick is MobEntity pm && pm.Engaged == m.Engaged ? pick : m;
        }

        // CMobController::Cast: face it, then Internal_Cast (an untargetable mob target refuses it)
        private bool MobCast(MobEntity m, BattleEntity target, ushort id, long now)
        {
            FaceTarget(m, target);
            if (IsUntargetable(target)) return false;
            if (!CanChangeState(m)) return false;
            return StartMagic(m, target, id, now);
        }

        // CMobController::OnCastStopped: the next spell a random half to whole magic cool away
        private void OnCastStopped(MobEntity m, long now)
        {
            int cool = m.GetMobMod(MobMod.MagicCool);
            int wait = cool / 2 < cool ? MobRng.Next(cool / 2, cool) : cool / 2;
            m.NextMagicMs = now + wait * 1000L;
        }

        // ---- the container's pickers --------------------------------------------------------------------------------

        // CMobSpellContainer::GetAvailable: affordable (or a free kind of spell, or under Manafont) and off recast
        private ushort? GetAvailableSpell(MobEntity m, ushort id)
        {
            var spell = SpellRow(id);
            if (spell == null) return null;
            bool free = spell.Skill is Spell.SkillNinjutsu or Spell.SkillSinging or Spell.SkillWind or Spell.SkillString or Spell.SkillGeomancy;
            bool affordable = spell.MpCost <= m.Mp || free || m.StatusEffects.HasStatusEffect(MagicFx.Manafont);
            return affordable && !RecastsOf(m).Has(RecastType.Magic, id) ? id : null;
        }

        private ushort? RandomAvailable(MobEntity m, List<ushort> list)
        {
            var available = new List<ushort>();
            foreach (var id in list)
                if (GetAvailableSpell(m, id) is ushort a) available.Add(a);
            return available.Count == 0 ? null : available[MobRng.Next(available.Count)];
        }

        // CMobSpellContainer::IsAnySpellAvailable
        private bool IsAnySpellAvailable(MobEntity m)
        {
            var c = m.SpellContainer;
            foreach (var list in new[] { c.GaList, c.DamageList, c.BuffList, c.DebuffList, c.HealList, c.NaList, c.RaiseList, c.SevereList })
                foreach (var id in list)
                    if (GetAvailableSpell(m, id) != null) return true;
            return false;
        }

        // CMobSpellContainer::GetAggroSpell: a ga spell by the ga chance, else a damage spell
        private ushort? GetAggroSpell(MobEntity m)
        {
            if (m.SpellContainer.HasGaSpells() && MobRng.Next(100) < m.GetMobMod(MobMod.GaChance)) return RandomAvailable(m, m.SpellContainer.GaList);
            return RandomAvailable(m, m.SpellContainer.DamageList);
        }

        // CMobSpellContainer::GetSpell: heal when hurt, na, severe, ga, buff by their chances, then the first bucket
        // with anything
        private ushort? GetSpellToCast(MobEntity m)
        {
            var c = m.SpellContainer;
            if (c.HasHealSpells() && m.Hpp <= m.GetMobMod(MobMod.HpHealChance) && MobRng.Next(100) < m.GetMobMod(MobMod.HealChance))
                return RandomAvailable(m, c.HealList);
            if (c.HasNaSpells() && MobRng.Next(100) < m.GetMobMod(MobMod.NaChance) && GetNaSpell(m) is ushort na) return na;
            if (c.HasSevereSpells() && MobRng.Next(100) < m.GetMobMod(MobMod.SevereSpellChance)) return RandomAvailable(m, c.SevereList);
            if (c.HasGaSpells() && MobRng.Next(100) < m.GetMobMod(MobMod.GaChance)) return RandomAvailable(m, c.GaList);
            if (c.HasBuffSpells() && MobRng.Next(100) < m.GetMobMod(MobMod.BuffChance) && GetBuffSpell(m) is ushort buff) return buff;
            if (c.HasDamageSpells()) return RandomAvailable(m, c.DamageList);
            if (c.HasDebuffSpells()) return RandomAvailable(m, c.DebuffList);
            if (c.HasBuffSpells() && GetBuffSpell(m) is ushort b) return b;
            if (c.HasGaSpells()) return RandomAvailable(m, c.GaList);
            if (c.HasHealSpells()) return RandomAvailable(m, c.HealList);
            return null;
        }

        // CMobSpellContainer::GetNaSpell: the cure for what ails it, if it knows it
        private ushort? GetNaSpell(MobEntity m)
        {
            var c = m.SpellContainer;
            var fx = m.StatusEffects;
            if (fx.HasStatusEffect(EffectId.Paralysis) && c.HasNaSpell(15)) return GetAvailableSpell(m, 15);
            if (fx.HasStatusEffect(EffectId.CurseI, EffectId.CurseII) && c.HasNaSpell(20)) return GetAvailableSpell(m, 20);
            if (fx.HasStatusEffectByFlag(EffectFlag.Erasable) && c.HasNaSpell(Spell.Erase)) return GetAvailableSpell(m, Spell.Erase);
            if (fx.HasStatusEffect(EffectId.Blindness) && c.HasNaSpell(16)) return GetAvailableSpell(m, 16);
            if (fx.HasStatusEffect(EffectId.Poison) && c.HasNaSpell(14)) return GetAvailableSpell(m, 14);
            return null;
        }

        // CMobSpellContainer::GetBuffSpellsFor: the castable buffs the target may take that it lacks (or has at a lower
        // tier)
        private List<ushort> GetBuffSpellsFor(MobEntity m, BattleEntity t)
        {
            var result = new List<ushort>();
            foreach (var id in m.SpellContainer.BuffList)
            {
                var spell = SpellRow(id);
                if (spell == null || GetAvailableSpell(m, id) == null) continue;
                if (t != m && !ValidSkillTarget(t, m, spell.ValidTargets)) continue;
                bool lacks = spell.StatusEffect is not ushort effect || t.StatusEffects.GetStatusEffect(effect) is not { } e ||
                             (spell.StatusEffectTier != 0 && e.Tier != 0 && e.Tier < spell.StatusEffectTier);
                if (lacks) result.Add(id);
            }
            return result;
        }

        // CMobSpellContainer::GetBuffSpell: a buff whose effect it does not have
        private ushort? GetBuffSpell(MobEntity m)
        {
            var candidates = new List<ushort>();
            foreach (var id in m.SpellContainer.BuffList)
            {
                var spell = SpellRow(id);
                if (spell == null) continue;
                if (spell.StatusEffect is not ushort effect || !m.StatusEffects.HasStatusEffect(effect)) candidates.Add(id);
            }
            return candidates.Count == 0 ? null : candidates[MobRng.Next(candidates.Count)];
        }
    }
}
