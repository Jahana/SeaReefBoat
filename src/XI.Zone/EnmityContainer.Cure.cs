using System;
using System.Collections.Generic;

namespace XI.Zone
{
    // CEnmityContainer's cure enmity, SetActive and SetCE / SetVE, with battleutils::GetEnmityModCure and
    // HandleTranquilHeart.
    public sealed partial class EnmityContainer
    {
        // merit.h, status_effect.h, trait.h
        private const ushort MeritEnmityIncrease = 320, MeritEnmityDecrease = 322, MeritMutedSoul = 2500;
        private const ushort EffectSouleater = 63;
        private const int TraitTranquilHeart = 114;
        private const int SkillHealingMagic = 33;

        // m_tameable: true until something other than a claim (UpdateEnmity's tameable argument) adds enmity; taking one
        // entry off the list makes it true again
        private bool tameable_ = true;
        public bool IsTameable => tameable_;

        // battleutils::GetEnmityModCure
        public static int EnmityModCure(int level)
        {
            if (level <= 10) return level + 10;
            if (level <= 50) return 20 + (level - 10) / 2;
            return (short)(40 + (level - 50) * 0.6);
        }

        // battleutils::HandleTranquilHeart: a char with the trait cuts its cure enmity by half a percent per 10 healing
        // magic skill, 25 percent at most
        public static float TranquilHeart(BattleEntity e)
        {
            float reductionPercent = 0.0f;
            if (e is CharData c && c.HasTrait(TraitTranquilHeart))
            {
                int healingSkill = CharStats.GetSkill(c, SkillHealingMagic);
                reductionPercent = healingSkill / 10.0f * 0.5f;
                reductionPercent = Math.Min(reductionPercent, 25.0f);
                reductionPercent /= 100.0f;
            }
            return reductionPercent;
        }

        // UpdateEnmityFromCure: fixed CE / VE if given, else the amount cured scaled by the cured target's level; the
        // enmity bonus and Tranquil Heart apply either way, and the entry is active
        public void UpdateEnmityFromCure(BattleEntity e, int level, int cureAmount, int fixedCe, int fixedVe)
        {
            if (!InRange(e) || IgnorePets(e)) return;
            int ce, ve;
            float bonus = Bonus(e);
            float tranquilHeartReduction = 1.0f - TranquilHeart(e);
            if (fixedCe > 0 || fixedVe > 0)
            {
                ce = (int)(fixedCe * bonus * tranquilHeartReduction);
                ve = (int)(fixedVe * bonus * tranquilHeartReduction);
            }
            else
            {
                cureAmount = Math.Max(cureAmount, 1);
                ce = (int)(40.0f / EnmityModCure(level) * cureAmount * bonus * tranquilHeartReduction);
                ve = (int)(240.0f / EnmityModCure(level) * cureAmount * bonus * tranquilHeartReduction);
            }
            if (list_.TryGetValue(e.Id, out var o))
            {
                o.CE = Math.Clamp(o.CE + ce, 0, EnmityCap);
                o.VE = Math.Clamp(o.VE + ve, 0, EnmityCap);
                o.Active = true;
            }
            else
            {
                list_[e.Id] = new Entry { Owner = e, CE = Math.Clamp(ce, 0, EnmityCap), VE = Math.Clamp(ve, 0, EnmityCap), Active = true };
                e.Notoriety.Add(holder_);
            }
        }

        // SetActive
        public void SetActive(uint id, bool active)
        {
            if (list_.TryGetValue(id, out var o)) o.Active = active;
        }

        // SetCE / SetVE: capped at the enmity cap, a base entry made first if there is none
        public void SetCE(BattleEntity e, int amount)
        {
            if (list_.TryGetValue(e.Id, out var o)) o.CE = Math.Min(amount, EnmityCap);
            else
            {
                AddBaseEnmity(e);
                if (list_.ContainsKey(e.Id)) SetCE(e, amount);
            }
        }

        public void SetVE(BattleEntity e, int amount)
        {
            if (list_.TryGetValue(e.Id, out var o)) o.VE = Math.Min(amount, EnmityCap);
            else
            {
                AddBaseEnmity(e);
                if (list_.ContainsKey(e.Id)) SetVE(e, amount);
            }
        }
    }
}
