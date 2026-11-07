using System.Collections.Generic;

namespace XI.Zone
{
    // CStatusEffectContainer::HandleAura: on each tick of an aura effect (EFFECTFLAG_AURA, e.g. a geomancer's
    // COLURE_ACTIVE on the caster or a luopan), the effect's sub effect goes for 4 s onto every ally or enemy within
    // 6 yalms (+ AURA_SIZE / 100) of the owner, or is refreshed there. A pet's or trust's aura works from its master's
    // side: the allies of a char are its party with their trusts, its enemies its notoriety container (never a
    // trust); a mob's allies are its link party, its enemies those on its enmity list.
    public sealed partial class ZoneHost
    {
        private const ushort AuraTargetAllies = 0, AuraTargetEnemies = 1;

        void IStatusEffectHost.HandleAura(BattleEntity owner, StatusEffect aura) => HandleAura(owner, aura);

        private void HandleAura(BattleEntity owner, StatusEffect aura)
        {
            BattleEntity entity = owner;
            float auraRange = 6.0f + owner.GetMod(Mod.AURA_SIZE) / 100.0f;
            if (owner is PetEntity or TrustEntity) entity = owner.PMaster;

            IEnumerable<BattleEntity> targets;
            if (entity is CharData c)
            {
                if (aura.Tier == AuraTargetAllies) targets = PartyWithTrusts(c);
                else if (aura.Tier == AuraTargetEnemies)
                {
                    var list = new List<BattleEntity>();
                    foreach (var enemy in c.Notoriety)
                        if (enemy is not TrustEntity) list.Add(enemy);
                    targets = list;
                }
                else return;
            }
            else if (entity is MobEntity mob && entity is not PetEntity)
            {
                if (aura.Tier == AuraTargetAllies) targets = PartyOf(mob);
                else if (aura.Tier == AuraTargetEnemies)
                {
                    var list = new List<BattleEntity>();
                    foreach (var e in mob.Enmity.Entries)
                        if (e.Owner != null) list.Add(e.Owner);
                    targets = list;
                }
                else return;
            }
            else return;

            foreach (var target in targets)
            {
                if (target == null || !SameArea(target, owner) || !SameArea(target, entity) ||
                    Distance(owner, target) > auraRange + HitboxSize(target) || target.Hp <= 0 || target is MobEntity { IsDead: true })
                    continue;
                ApplyAuraEffect(target, aura);
            }
        }

        // the sub effect refreshed when it came from an aura (ALWAYS_EXPIRING), its power changed with the aura's
        // (Ecliptic Attrition); else a new one, silent and without a loss message
        private void ApplyAuraEffect(BattleEntity target, StatusEffect aura)
        {
            var host = (IStatusEffectHost)this;
            var effect = target.StatusEffects.GetStatusEffect((ushort)aura.SubId);
            if (effect != null && effect.HasFlag(EffectFlag.AlwaysExpiring))
            {
                effect.SetStartTime(NowMs());
                if (effect.Power != aura.SubPower)
                {
                    host.OnEffectLose(target, effect);
                    effect.Power = aura.SubPower;
                    host.OnEffectGain(target, effect);
                }
                return;
            }
            ushort icon = aura.SubIcon > 0 ? aura.SubIcon : (ushort)aura.SubId;
            target.StatusEffects.AddStatusEffect(
                new StatusEffect((ushort)aura.SubId, icon, aura.SubPower, 3000, 4000,
                                 flags: EffectFlag.NoLossMessage | EffectFlag.AlwaysExpiring),
                EffectNotice.Silent);
        }
    }
}
