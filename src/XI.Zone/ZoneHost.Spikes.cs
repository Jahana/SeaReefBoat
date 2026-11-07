using System;
using XI.Script;

namespace XI.Zone
{
    // battleutils::HandleSpikesDamage, after a melee swing's additional effect on a hit, guard or block: Retaliation
    // (the defender, engaged and facing the attacker, counters at half its hit rate, unless the attacker has Perfect
    // Dodge; a shadow may take it), else the defender's spikes (SPIKES / SPIKES_DMG from a spell, Reprisal's share of a
    // block or a mob script's onSpikesDamage under AUTO_SPIKES) as magic damage to the attacker (Dread Spikes drains it,
    // up to its subpower; Reprisal only on a shield block), else the defender's gear's spikes (ITEM_SUBEFFECT on the
    // sub, body, legs, head, hands or feet: a chance by the level difference, a curse or damage), and the status the
    // spikes carry (Curse Spikes' curse, Ice Spikes' paralysis, Shock Spikes' stun). Battuta's parry spikes
    // (HandleParrySpikesDamage) on a parried swing. CalculateSpikeDamage, HandleSpikesEquip, HandleSpikesStatusEffect.
    public sealed partial class ZoneHost
    {
        // xi::StatusEffect
        private const ushort EffectInvincible = 50, EffectSentinel = 62, EffectRetaliation = 405, EffectBattuta = 570;

        // SPIKES / ActionReactKind
        private const byte SpikeNone = 0, SpikeBlaze = 1, SpikeIce = 2, SpikeDread = 3, SpikeCurse = 4, SpikeShock = 5, SpikeReprisal = 6;

        // msg_basic.h
        private const ushort MsgSpikesEffectDmg = 44, MsgSpikesEffectHpDrain = 132, MsgStatusSpikes = 374, MsgSpikesEffectHeal = 383,
            MsgRetaliateShadowAbsorbs = 535, MsgRetaliateDamage = 536;

        // xi::DamageType
        private const byte DamageTypeLight = 12, DamageTypeDark = 13;

        // the gear that can carry spikes, in the C++'s order
        private static readonly int[] SpikesGearSlots = { CharStats.SlotSub, 5, 7, 4, 6, 8 }; // sub, body, legs, head, hands, feet

        // GetSpikesDamageType: the action packet's animation order
        private static byte SpikesDamageType(byte spikes) => spikes switch
        {
            1 => 6,   // blaze: fire
            2 => 7,   // ice
            3 => 13,  // dread: dark
            5 => 10,  // shock: thunder
            6 => 12,  // reprisal: light
            7 => 8,   // wind
            8 => 9,   // earth
            9 => 11,  // water
            10 => 13, // death: dark
            _ => 0,   // curse and the rest: none
        };

        private bool HandleSpikesDamage(BattleEntity attacker, BattleEntity defender, ActionResult r, int damage, long now)
        {
            r.SpikesEffect = (byte)defender.GetMod(Mod.SPIKES);
            r.SpikesMessage = MsgSpikesEffectDmg;
            r.SpikesParam = Math.Max(defender.GetMod(Mod.SPIKES_DMG), (short)0);

            var melee = MeleeFor();
            // Retaliation: by the defender's accuracy against the attacker's evasion; a miss shows nothing
            if (!attacker.StatusEffects.HasStatusEffect(EffectPerfectDodge) && defender.StatusEffects.HasStatusEffect(EffectRetaliation) &&
                DefenseIsEngaged(defender) && melee.HitRate(defender, attacker, 0) / 2 > MobRng.Next(100) && Facing(defender, attacker))
            {
                r.Resolution = ResolutionHit;
                r.SpikesEffect = ReactCounter;
                if (IsAbsorbByShadow(attacker, defender)) r.SpikesMessage = MsgRetaliateShadowAbsorbs;
                else
                {
                    byte skillType = Skill.None;
                    if (defender is CharData dc)
                    {
                        // m_Weapons[SLOT_MAIN] is always a weapon (the unarmed one hand-to-hand)
                        skillType = dc.MainWeapon?.Skill ?? Skill.HandToHand;
                        TrySkillUp(dc, skillType, attacker.MLvl, false, false);
                    }
                    bool crit = melee.CritHitRate(defender, attacker, CharStats.SlotMain, true) > MobRng.Next(100);
                    float ratio = melee.DamageRatio(defender, attacker, crit, skillType, CharStats.SlotMain);
                    ushort dmg = (ushort)(uint)((Melee.MainWeaponDmg(defender) + Melee.FStr(defender, attacker)) * ratio);
                    // (the C++ casts any defender to a char here; only a char's weapon has the multipliers)
                    if (defender is CharData mc)
                        dmg = (ushort)PhysicalDamageMultiplier(mc, mc.MainWeapon, dmg, PhysicalAttackType.Normal, CharStats.SlotMain, false, MobRng);
                    ushort bonus = (ushort)MathF.Floor(dmg * (defender.GetMod(Mod.RETALIATION) / 100.0f));
                    dmg = (ushort)(dmg + bonus);

                    // TP and stoneskin are handled inside TakePhysicalDamage
                    r.SpikesMessage = MsgRetaliateDamage;
                    r.SpikesParam = SwingTakePhysicalDamage(defender, attacker, dmg, CharStats.SlotMain, false, true, true, now);
                }
            }
            // spikes from spells or auto-spikes (scripted) effects
            else if (r.SpikesEffect != SpikeNone)
            {
                // (a pet or a trust is no TYPE_MOB)
                bool autoSpikes = defender is MobEntity dm and not PetEntity and not TrustEntity && dm.GetMobMod(MobMod.AutoSpikes) > 0;
                if (autoSpikes) OnSpikesDamage((MobEntity)defender, attacker, r, r.SpikesParam);

                int spikesDamage = CalculateSpikeDamage(attacker, defender, r, (ushort)Math.Abs(damage));
                if (spikesDamage > 0)
                {
                    spikesDamage = Math.Max(spikesDamage - attacker.GetMod(Mod.PHALANX), 0);
                    spikesDamage = HandleOneForAll(attacker, spikesDamage);
                    spikesDamage = attacker.StatusEffects.HandleStoneskin(spikesDamage);
                }
                // the packet's spikes param is unsigned: a heal goes in as its amount, up to the HP missing
                r.SpikesParam = spikesDamage < 0 ? (ushort)Math.Clamp(-spikesDamage, 0, attacker.MaxHp - attacker.Hp) : (ushort)spikesDamage;

                if (!autoSpikes)
                {
                    switch (r.SpikesEffect)
                    {
                        case SpikeBlaze:
                        case SpikeIce:
                        case SpikeShock:
                            BattleTakeDamage(attacker, spikesDamage, defender, AttackTypeMagical, SpikesDamageType(r.SpikesEffect));
                            break;
                        case SpikeDread:
                            if (IsUndead(attacker))
                            {
                                r.SpikesEffect = SpikeNone;
                                return false;
                            }
                            if (defender.IsAlive)
                            {
                                // (bg-wiki Dread Spikes) the subpower is what is left to drain, counted in the hit's
                                // damage before MDT; at 0 the effect ends
                                if (defender.StatusEffects.GetStatusEffect(EffectId.DreadSpikes) is { } dread)
                                {
                                    int remainingDrain = dread.SubPower;
                                    if (remainingDrain - Math.Abs(damage) <= 0)
                                    {
                                        spikesDamage = Math.Min(spikesDamage, remainingDrain);
                                        r.SpikesParam = (ushort)spikesDamage;
                                        defender.StatusEffects.DelStatusEffect(EffectId.DreadSpikes);
                                    }
                                    else dread.SubPower = (ushort)(remainingDrain - Math.Abs(damage));
                                }
                                // no HP when the spikes' damage was absorbed
                                if (spikesDamage > 0)
                                {
                                    r.SpikesMessage = MsgSpikesEffectHpDrain;
                                    BattleAddHp(defender, spikesDamage);
                                }
                            }
                            BattleTakeDamage(attacker, spikesDamage, defender, AttackTypeMagical, DamageTypeDark);
                            break;
                        case SpikeReprisal:
                            // only on a shield block
                            if (r.Resolution == ResolutionBlock)
                                BattleTakeDamage(attacker, spikesDamage, defender, AttackTypeMagical, DamageTypeLight);
                            else
                            {
                                r.SpikesEffect = SpikeNone;
                                return false;
                            }
                            break;
                    }
                }

                HandleSpikesStatusEffect(attacker, defender, r);
                if (attacker is MobEntity spiked)
                {
                    DirtyExp(spiked, defender);
                    if (spiked.Hp <= 0) ClaimMob(spiked, defender, now);
                }
                return true;
            }
            // the gear's spikes
            else if (defender.GetMod(Mod.ITEM_SUBEFFECT) > 0)
            {
                if (defender is CharData pcDef)
                {
                    foreach (int slot in SpikesGearSlots)
                    {
                        if (pcDef.Equipped[slot] is not { } item || item.Weapon != null) continue;
                        r.SpikesEffect = SpikeNone;
                        int spikesType = CharStats.ScaledItemModifier(pcDef, item, Mod.ITEM_SUBEFFECT);
                        if (spikesType > 0 && spikesType < 7) r.SpikesEffect = (byte)spikesType;
                        r.SpikesParam = CharStats.ScaledItemModifier(pcDef, item, Mod.ITEM_ADDEFFECT_DMG);
                        byte chance = (byte)CharStats.ScaledItemModifier(pcDef, item, Mod.ITEM_ADDEFFECT_CHANCE);
                        // (the C++'s m_HiPCLvl update casts this char defender to a mob: it never happens)
                        if (r.SpikesEffect != SpikeNone && HandleSpikesEquip(attacker, defender, r, (byte)r.SpikesParam, r.SpikesEffect, chance))
                            return true;
                    }
                }
            }
            else if (r.SpikesEffect == SpikeNone)
            {
                r.SpikesParam = 0;
                r.SpikesMessage = 0;
            }
            return false;
        }

        // HandleParrySpikesDamage: Battuta's PARRY_SPIKES / PARRY_SPIKES_DMG on a parried swing
        private bool HandleParrySpikesDamage(BattleEntity attacker, BattleEntity defender, ActionResult r, int damage, long now)
        {
            r.SpikesEffect = (byte)defender.GetMod(Mod.PARRY_SPIKES);
            r.SpikesMessage = MsgSpikesEffectDmg;
            r.SpikesParam = Math.Max(defender.GetMod(Mod.PARRY_SPIKES_DMG), (short)0);
            if (r.SpikesEffect == SpikeNone) return false;

            int spikesDamage = CalculateSpikeDamage(attacker, defender, r, (ushort)Math.Abs(damage));
            if (spikesDamage > 0)
            {
                spikesDamage = Math.Max(spikesDamage - attacker.GetMod(Mod.PHALANX), 0);
                spikesDamage = HandleOneForAll(attacker, spikesDamage);
                spikesDamage = attacker.StatusEffects.HandleStoneskin(spikesDamage);
            }
            r.SpikesParam = spikesDamage < 0 ? (ushort)Math.Clamp(-spikesDamage, 0, attacker.MaxHp - attacker.Hp) : (ushort)spikesDamage;
            BattleTakeDamage(attacker, spikesDamage, defender, AttackTypeMagical, SpikesDamageType(r.SpikesEffect));
            if (attacker is MobEntity spiked)
            {
                DirtyExp(spiked, defender);
                if (spiked.Hp <= 0) ClaimMob(spiked, defender, now);
            }
            return true;
        }

        // HandleSpikesEquip: a piece of gear's spikes, by its chance and the level difference (2% a level, up to 5)
        private bool HandleSpikesEquip(BattleEntity attacker, BattleEntity defender, ActionResult r, byte damage, byte spikesType, byte chance)
        {
            int lvlDiff = Math.Clamp(defender.MLvl - attacker.MLvl, -5, 5) * 2;
            if (MobRng.Next(100) < chance + lvlDiff)
            {
                if (spikesType == SpikeCurse)
                {
                    r.SpikesMessage = MsgStatusSpikes;
                    r.SpikesParam = EffectId.CurseI;
                }
                else
                {
                    int ratio = Math.Clamp(damage / 4, 1, 255);
                    int spikesDamage = CalculateSpikeDamage(attacker, defender, r, (ushort)(damage - MobRng.Next(ratio) + MobRng.Next(ratio)));
                    if (spikesDamage > 0)
                    {
                        // (as the C++, the param stays the item's base damage)
                        spikesDamage = Math.Max(spikesDamage - attacker.GetMod(Mod.PHALANX), 0);
                        spikesDamage = HandleOneForAll(attacker, spikesDamage);
                        spikesDamage = attacker.StatusEffects.HandleStoneskin(spikesDamage);
                    }
                    else if (spikesDamage < 0) r.SpikesParam = (ushort)Math.Clamp(-spikesDamage, 0, attacker.MaxHp - attacker.Hp);
                    else r.SpikesParam = (ushort)spikesDamage;
                    BattleTakeDamage(attacker, spikesDamage, defender, AttackTypeMagical, SpikesDamageType(spikesType));
                }
                HandleSpikesStatusEffect(attacker, defender, r);
                return true;
            }
            r.SpikesEffect = SpikeNone;
            r.SpikesParam = 0;
            r.SpikesMessage = 0;
            return false;
        }

        // HandleSpikesStatusEffect: Curse Spikes curse; Ice Spikes paralyze and Shock Spikes stun by chance
        private void HandleSpikesStatusEffect(BattleEntity attacker, BattleEntity defender, ActionResult r)
        {
            int lvlDiff = defender != null ? Math.Clamp(defender.MLvl - attacker.MLvl, -5, 5) * 2 : 0;
            var fx = attacker.StatusEffects;
            switch (r.SpikesEffect)
            {
                case SpikeCurse:
                    if (!fx.HasStatusEffect(EffectId.CurseI)) fx.AddStatusEffect(new StatusEffect(EffectId.CurseI, EffectId.CurseI, 15, 0, 180_000));
                    break;
                case SpikeIce:
                    if (MobRng.Next(100) < 20 + lvlDiff && !fx.HasStatusEffect(EffectId.Paralysis))
                        fx.AddStatusEffect(new StatusEffect(EffectId.Paralysis, EffectId.Paralysis, 20, 0, 30_000));
                    break;
                case SpikeShock:
                    if (MobRng.Next(100) < 30 + lvlDiff && !fx.HasStatusEffect(EffectId.Stun))
                        fx.AddStatusEffect(new StatusEffect(EffectId.Stun, EffectId.Stun, 1, 0, 3_000));
                    break;
            }
        }

        // CalculateSpikeDamage: SPIKES_DMG_BONUS, Dread Spikes' drain of the hit's damage, and MagicDmgTaken for the
        // attacker taking it; a negative result is a heal
        private int CalculateSpikeDamage(BattleEntity attacker, BattleEntity defender, ActionResult r, ushort damageTaken)
        {
            int element = SpikesDamageType(r.SpikesEffect) - DamageTypeElemental;
            int damage = r.SpikesParam;
            if (defender.GetMod(Mod.SPIKES_DMG_BONUS) > 0) damage = (int)(damage * (1 + defender.GetMod(Mod.SPIKES_DMG_BONUS) / 100.0f));
            // drain the same as the damage taken
            if (r.SpikesEffect == SpikeDread) damage = damageTaken;
            if (r.SpikesEffect > 8)
            {
                log_($"CalculateSpikeDamage: Spike Element from PDefender ({defender.Name}, id {defender.Id}) out of range, got {element}. Setting to Fire.");
                element = 1;
            }
            // (curse spikes have no element: the C++ reads past its tables with (uint8)(0 - 5); no element here)
            if (element < 1 || element > 8) element = 0;
            damage = MagicDmgTaken(attacker, damage, element);
            if (damage < 0) r.SpikesMessage = MsgSpikesEffectHeal;
            return damage;
        }

        // luautils::OnSpikesDamage: the mob script's onSpikesDamage(mob, attacker, damage) -> (spikes, message, param)
        private void OnSpikesDamage(MobEntity defender, BattleEntity attacker, ActionResult r, int damage)
        {
            if (lua_ == null) return;
            int top = lua_.Top;
            try
            {
                PushMobFunction(defender, "onSpikesDamage");
                if (!lua_.IsFunction(-1)) return;
                lua_.PushObject(defender);
                lua_.PushObject(attacker);
                lua_.Push((double)damage);
                lua_.Call(3, 3);
                // get<ActionReactKind> and get<MsgBasic> are enums, the param an int32
                r.SpikesEffect = lua_.IsNumber(top + 1) ? (byte)lua_.ToEnum(top + 1) : SpikeNone;
                r.SpikesMessage = lua_.IsNumber(top + 2) ? (ushort)lua_.ToEnum(top + 2) : (ushort)0;
                r.SpikesParam = lua_.IsNumber(top + 3) ? (int)lua_.ToIntegral(top + 3) : 0;
            }
            catch (LuaException e)
            {
                ScriptError("luautils::onSpikesDamage", e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }
    }
}
