using System;
using System.Collections.Generic;

namespace XI.Zone
{
    // Skillchains (battleutils): GetSkillchainSubeffect, GetSkillchainTier, the skillchain_map pairs, FormSkillchain,
    // GetSkillChainEffect (the SKILLCHAIN effect as the opener's properties at tier 0, the 3 s window, Chainbound /
    // Konzen-Ittai, the resonance's count capped at 5), GetSkillchainMagicElement, TakeSkillchainDamage (the damage
    // through xi.combat.skillchain.calculateSkillchainDamage, the claim, the DAMAGE-flag effects off, a sitting char
    // stood up, the mob's enmity) and action_result_t::recordSkillchain. Called from the weapon skill finish
    // (CCharEntity::OnWeaponSkillFinished, with map.WS_POINTS_SKILLCHAIN) and the mob skill finish
    // (CBattleEntity::OnMobSkillFinished). Not ported: blue magic and the ninjutsu / elemental chains off a spell
    // (no magic), pets' and trusts' skills, Trick Attack's taChar, the automaton / gambit readers.
    public sealed partial class ZoneHost
    {
        private const ushort EffectSkillchain = 799, EffectChainbound = 798;

        // enum SKILLCHAIN_ELEMENT
        public const byte ScNone = 0, ScTransfixion = 1, ScCompression = 2, ScLiquefaction = 3, ScScission = 4,
            ScReverberation = 5, ScDetonation = 6, ScInduration = 7, ScImpaction = 8, ScGravitation = 9, ScDistortion = 10,
            ScFusion = 11, ScFragmentation = 12, ScLight = 13, ScDarkness = 14, ScLightII = 15, ScDarknessII = 16;

        // enum class ActionProcSkillChain
        public const byte ProcScNone = 0, ProcScLight = 1, ProcScDarkness = 2, ProcScGravitation = 3, ProcScFragmentation = 4,
            ProcScDistortion = 5, ProcScFusion = 6, ProcScCompression = 7, ProcScLiquefaction = 8, ProcScInduration = 9,
            ProcScReverberation = 10, ProcScTransfixion = 11, ProcScScission = 12, ProcScDetonation = 13, ProcScImpaction = 14,
            ProcScRadiance = 15, ProcScUmbra = 16;

        // battleutils::GetSkillchainSubeffect
        public static byte GetSkillchainSubeffect(byte skillchain)
        {
            if (skillchain > ScDarknessII) return ProcScNone;
            return SkillchainEffects[skillchain];
        }

        private static readonly byte[] SkillchainEffects =
        {
            ProcScNone, ProcScTransfixion, ProcScCompression, ProcScLiquefaction, ProcScScission, ProcScReverberation,
            ProcScDetonation, ProcScInduration, ProcScImpaction, ProcScGravitation, ProcScDistortion, ProcScFusion,
            ProcScFragmentation, ProcScLight, ProcScDarkness, ProcScLight, ProcScDarkness,
        };

        // battleutils::GetSkillchainTier
        public static byte GetSkillchainTier(byte skillchain)
        {
            if (skillchain > ScDarknessII) return 0;
            return SkillchainTiers[skillchain];
        }

        private static readonly byte[] SkillchainTiers = { 0, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 4, 4 };

        // skillchain_map: (the skill's property, the resonance's) to the skillchain formed
        private static readonly Dictionary<(byte, byte), byte> SkillchainMap = new Dictionary<(byte, byte), byte>
        {
            // Level 3 Pairs
            [(ScLight, ScLight)] = ScLightII,
            [(ScDarkness, ScDarkness)] = ScDarknessII,

            // Level 2 Pairs
            [(ScDistortion, ScGravitation)] = ScDarkness,
            [(ScFragmentation, ScGravitation)] = ScFragmentation,

            [(ScGravitation, ScDistortion)] = ScDarkness,
            [(ScFusion, ScDistortion)] = ScFusion,

            [(ScGravitation, ScFusion)] = ScGravitation,
            [(ScFragmentation, ScFusion)] = ScLight,

            [(ScDistortion, ScFragmentation)] = ScDistortion,
            [(ScFusion, ScFragmentation)] = ScLight,

            // Level 1 Pairs > Level 2 Skillchain
            [(ScScission, ScTransfixion)] = ScDistortion,
            [(ScImpaction, ScLiquefaction)] = ScFusion,
            [(ScCompression, ScDetonation)] = ScGravitation,
            [(ScReverberation, ScInduration)] = ScFragmentation,

            // Level 1 Pairs
            [(ScCompression, ScTransfixion)] = ScCompression,
            [(ScReverberation, ScTransfixion)] = ScReverberation,

            [(ScTransfixion, ScCompression)] = ScTransfixion,
            [(ScDetonation, ScCompression)] = ScDetonation,

            [(ScScission, ScLiquefaction)] = ScScission,

            [(ScLiquefaction, ScScission)] = ScLiquefaction,
            [(ScReverberation, ScScission)] = ScReverberation,
            [(ScDetonation, ScScission)] = ScDetonation,

            [(ScInduration, ScReverberation)] = ScInduration,
            [(ScImpaction, ScReverberation)] = ScImpaction,

            [(ScScission, ScDetonation)] = ScScission,

            [(ScCompression, ScInduration)] = ScCompression,
            [(ScImpaction, ScInduration)] = ScImpaction,

            [(ScLiquefaction, ScImpaction)] = ScLiquefaction,
            [(ScDetonation, ScImpaction)] = ScDetonation,
        };

        // battleutils::FormSkillchain
        public static byte FormSkillchain(List<byte> resonance, List<byte> skill)
        {
            foreach (var resonanceElement in resonance)
                foreach (var skillElement in skill)
                    if (SkillchainMap.TryGetValue((skillElement, resonanceElement), out var skillchain))
                        return skillchain;
            return ScNone;
        }

        // battleutils::GetSkillChainEffect
        private byte GetSkillChainEffect(BattleEntity defender, byte primary, byte secondary, byte tertiary)
        {
            var scEffect = defender.StatusEffects.GetStatusEffect(EffectSkillchain, 0);
            var cbEffect = defender.StatusEffects.GetStatusEffect(EffectChainbound, 0);
            byte skillchain = ScNone;
            ushort combinedProperties = (ushort)(primary | (secondary << 4) | (tertiary << 8));
            long now = NowMs();

            if (scEffect == null && cbEffect == null)
            {
                // No effect exists, apply an effect using the weaponskill ID as the power with a tier of 0.
                defender.StatusEffects.AddStatusEffect(new StatusEffect(EffectSkillchain, 0, combinedProperties, 0, 10000));
                return ProcScNone;
            }

            var resonanceProperties = new List<byte>();
            var skillProperties = new List<byte> { primary, secondary, tertiary };

            // Chainbound active on target
            if (cbEffect != null)
            {
                if (cbEffect.StartMs + 2000 < now)
                {
                    // Konzen-Ittai
                    if (cbEffect.Power > 1)
                    {
                        resonanceProperties.Add(ScLight);
                        resonanceProperties.Add(ScDarkness);
                        resonanceProperties.Add(ScGravitation);
                        resonanceProperties.Add(ScFragmentation);
                        resonanceProperties.Add(ScDistortion);
                        resonanceProperties.Add(ScFusion);
                    }
                    resonanceProperties.Add(ScLiquefaction);
                    resonanceProperties.Add(ScInduration);
                    resonanceProperties.Add(ScReverberation);
                    resonanceProperties.Add(ScImpaction);
                    resonanceProperties.Add(ScCompression);

                    skillchain = FormSkillchain(resonanceProperties, skillProperties);
                }
                defender.StatusEffects.AddStatusEffect(new StatusEffect(EffectSkillchain, 0, combinedProperties, 0, 10000));
                defender.StatusEffects.DelStatusEffect(EffectChainbound);
                scEffect = defender.StatusEffects.GetStatusEffect(EffectSkillchain, 0);
            }
            // Previous effect exists
            else if (scEffect != null && scEffect.StartMs + 3000 < now)
            {
                if (scEffect.Tier == 0)
                {
                    if (scEffect.Power == 0)
                    {
                        log_("PSCEffect Power was 0.");
                        return ProcScNone;
                    }

                    // Previous effect is an opening effect, meaning the power is
                    // actually the ID of the opening weaponskill.  We need all 3
                    // of the possible skillchain properties on the initial link.
                    int properties = scEffect.Power;
                    resonanceProperties.Add((byte)(properties & 0b1111));
                    resonanceProperties.Add((byte)((properties >> 4) & 0b1111));
                    resonanceProperties.Add((byte)((properties >> 8) & 0b1111));
                    skillchain = FormSkillchain(resonanceProperties, skillProperties);
                }
                else
                {
                    // Previous effect is not an opening effect, meaning the power is
                    // The skill chain ID resonating.
                    resonanceProperties.Add((byte)scEffect.Power);
                    skillchain = FormSkillchain(resonanceProperties, skillProperties);
                }
            }

            if (scEffect == null) return ProcScNone;

            if (skillchain != ScNone)
            {
                scEffect.SetStartTime(now);
                scEffect.DurationMs -= 1000;
                scEffect.Tier = GetSkillchainTier(skillchain);
                scEffect.Power = skillchain;
                scEffect.SubPower = (ushort)Math.Min(scEffect.SubPower + 1, 5); // Linked, limited to 5

                return GetSkillchainSubeffect(skillchain);
            }

            scEffect.SetStartTime(now);
            scEffect.DurationMs = 10000;
            scEffect.Tier = 0;
            scEffect.Power = combinedProperties;
            scEffect.SubPower = 0;

            return ProcScNone;
        }

        // battleutils::GetSkillchainMagicElement (element ids: fire 1 .. dark 8)
        public static byte[] GetSkillchainMagicElement(byte skillchain) => skillchain switch
        {
            ScTransfixion => new byte[] { 7 },
            ScCompression => new byte[] { 8 },
            ScLiquefaction => new byte[] { 1 },
            ScScission => new byte[] { 4 },
            ScReverberation => new byte[] { 6 },
            ScDetonation => new byte[] { 3 },
            ScInduration => new byte[] { 2 },
            ScImpaction => new byte[] { 5 },
            ScGravitation => new byte[] { 8, 4 },
            ScDistortion => new byte[] { 6, 2 },
            ScFusion => new byte[] { 7, 1 },
            ScFragmentation => new byte[] { 3, 5 },
            ScLight or ScLightII => new byte[] { 7, 1, 3, 5 },
            ScDarkness or ScDarknessII => new byte[] { 8, 4, 6, 2 },
            _ => Array.Empty<byte>(),
        };

        // battleutils::TakeSkillchainDamage: a Trick Attack's taChar takes the mob's enmity
        private int TakeSkillchainDamage(BattleEntity attacker, BattleEntity defender, int lastSkillDamage, BattleEntity taChar = null)
        {
            if (attacker == null || defender == null)
            {
                log_("battleutils::TakeSkillchainDamage() - PAttacker or PDefender was null.");
                return 0;
            }

            // Call out to lua for all damage functions. Also processes actual damage application.
            int damage = WsCallGlobalInt("xi.combat.skillchain.calculateSkillchainDamage", attacker, defender, (double)lastSkillDamage);

            if (attacker is CharData ac && defender is MobEntity claimed) ClaimMob(claimed, ac, NowMs());

            defender.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Damage);

            switch (defender)
            {
                case CharData pc:
                    if (pc.Animation == Anim.Sit || (pc.Animation >= Anim.Sitchair0 && pc.Animation <= Anim.Sitchair10))
                    {
                        pc.Animation = Anim.None;
                        pc.UpdateMask |= CharStats.UpdateHp;
                    }
                    break;
                case MobEntity mob:
                    // assume negative damage (healing) deals the same enmity as dealing damage
                    mob.Enmity.UpdateEnmityFromDamage(taChar ?? attacker, Math.Abs(damage));
                    break;
            }

            return damage;
        }

        // action_result_t::recordSkillchain
        private static void RecordSkillchain(ActionResult r, byte effect, int dmg)
        {
            short d = (short)dmg;
            if (d < 0)
            {
                // Absorbs damage
                r.AddEffectParam = -d;
                r.AddEffectMessage = (ushort)(384 + effect);
            }
            else
            {
                r.AddEffectParam = d;
                r.AddEffectMessage = (ushort)(287 + effect);
            }
            r.AdditionalEffect = effect;
        }

        // The skillchain step of a weapon skill's or a mob skill's primary target: GetSkillChainEffect only on a live
        // target and a skill with a property, since it alters the resonance; returns the effect
        private byte SkillchainOnHit(BattleEntity attacker, BattleEntity target, ActionResult r, int damage, byte primary,
            byte secondary, byte tertiary)
        {
            if (target.Hp <= 0 || primary == 0) return ProcScNone;
            byte effect = GetSkillChainEffect(target, primary, secondary, tertiary);
            if (effect != ProcScNone) RecordSkillchain(r, effect, TakeSkillchainDamage(attacker, target, damage));
            return effect;
        }
    }
}
