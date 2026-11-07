using System;
using System.Collections.Generic;
using XI.Script;

namespace XI.Zone
{
    // The combat-stat CLuaBaseEntity methods the weapon skill, mob skill and spell scripts read: the stats, weapon
    // damage and delay, facing, hitboxes, enmity and the TP / MP spends. There are no pets or trusts yet, so the pet
    // checks are always false.
    public sealed partial class ZoneHost
    {
        private const ushort EffectFirestorm = 178, EffectHailstorm = 179, EffectWindstorm = 180, EffectSandstorm = 181,
                             EffectThunderstorm = 182, EffectRainstorm = 183, EffectAurorastorm = 184, EffectVoidstorm = 185;

        // xi::Weather
        private const ushort WeatherNone = 0, WeatherHotSpell = 4, WeatherRain = 6, WeatherDustStorm = 8, WeatherWind = 10,
                             WeatherSnow = 12, WeatherThunder = 14, WeatherAuroras = 16, WeatherGloom = 18;

        private void RegisterCombatBindings(Dictionary<string, LuaFunction> m)
        {
            m["getStat"] = l => Ret(l, Battle(l) is { } b ? GetStat(b, (Mod)l.ToIntArg(2), l) : 0);

            // weapon damage and rank
            m["getWeaponDmg"] = l => Ret(l, Battle(l) is { } b ? Melee.MainWeaponDmg(b) : 0);
            m["getWeaponDmgRank"] = l => Ret(l, Battle(l) is { } b ? Melee.MainWeaponRank(b) : 0);
            m["getOffhandDmg"] = l => Ret(l, Battle(l) is { } b ? Melee.SubWeaponDmg(b) : 0);
            m["getOffhandDmgRank"] = l => Ret(l, Battle(l) is { } b ? SubWeaponRank(b) : 0);
            m["getRangedDmg"] = l => Ret(l, Battle(l) is { } b ? RangedWeaponDmg(b) : 0);
            m["getRangedDmgRank"] = l => Ret(l, Battle(l) is { } b ? RangedWeaponRank(b) : 0);

            m["getACC"] = l =>
            {
                if (Battle(l) is not { } b) return Ret(l, 0);
                int attackNumber = l.IsNumber(2) ? (byte)l.ToIntegral(2) : 0;
                return Ret(l, (ushort)MeleeFor().Acc(b, attackNumber));
            };
            m["getRACC"] = l =>
            {
                if (Entity(l) is BattleEntity b) return Ret(l, Racc(b));
                log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                return Ret(l, 0);
            };
            m["getEVA"] = l =>
            {
                if (Entity(l) is BattleEntity b) return Ret(l, Melee.Eva(b));
                log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                return Ret(l, 0);
            };

            // delays
            m["getBaseDelay"] = l => Ret(l, Entity(l) is BattleEntity b ? Melee.BaseDelay(b) : 0);
            m["getBaseWeaponDelay"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return Ret(l, 0);
                }
                // getEquip: the item in the slot, not the unarmed weapon
                var weapon = CharStats.WeaponIn(c, l.ToIntArg(2));
                return Ret(l, weapon?.Equip != null ? weapon.Delay : 0);
            };
            m["getBaseRangedDelay"] = l => Ret(l, Entity(l) is BattleEntity b ? BaseRangedDelay(b) : 0);

            // the entity's kind
            m["isPet"] = l => Ret(l, Entity(l) is PetEntity);
            m["isAvatar"] = l => Ret(l, Entity(l) is PetEntity p && PetId.IsAvatar(p.PetId));
            m["isTrust"] = l => Ret(l, Entity(l) is TrustEntity);
            m["isJugPet"] = l => Ret(l, Entity(l) is PetEntity p && PetId.IsJugPet(p.PetId));
            m["isTandemActive"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return Ret(l, false);
                }
                return Ret(l, Melee.IsTandemActive(b));
            };

            // facing: the cone defaults to 64
            m["isFacing"] = l => Ret(l, ConeCheck(l, (a, b, half) => Math.Abs(FacingAngle(a, b)) < half));
            m["isInfront"] = l => Ret(l, ConeCheck(l, (a, b, half) => Math.Abs(FacingAngle(b, a)) < half));
            m["isBehind"] = l => Ret(l, ConeCheck(l, (a, b, half) => Math.Abs(FacingAngle(b, a)) > 128 - half));
            m["isBeside"] = l => Ret(l, ConeCheck(l, (a, b, half) =>
            {
                int diff = Math.Abs(FacingAngle(b, a));
                return diff > 64 - half && diff < 64 + half;
            }));

            m["getAllegiance"] = l => Ret(l, Entity(l) is BattleEntity b ? EnmityContainer.AllegianceOf(b) : 0);
            m["getHitboxSize"] = l => Ret(l, Entity(l) is BattleEntity b ? HitboxSize(b) : 0);
            m["getMeleeRange"] = l =>
            {
                if (Entity(l) is not BattleEntity b || l.ToObject(2) is not BattleEntity target)
                {
                    log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                    return Ret(l, 0);
                }
                return Ret(l, HitboxSize(b) + 2.0f + HitboxSize(target));
            };

            // the weapons
            m["getWeaponSkillType"] = l =>
            {
                if (Battle(l) is not { } b) return Ret(l, 0);
                int slot = l.ToIntArg(2);
                if (slot > CharStats.SlotAmmo)
                {
                    log_("lua::getWeaponSkillType :: Invalid slot specified!");
                    return Ret(l, 0);
                }
                return Ret(l, b switch
                {
                    CharData c => CharStats.WeaponIn(c, slot)?.Skill ?? 0,
                    _ => slot == CharStats.SlotMain ? Melee.MainSkill(b) : 0,
                });
            };
            m["getWeaponSubSkillType"] = l =>
            {
                int slot = l.ToIntArg(2);
                if (slot > CharStats.SlotAmmo)
                {
                    log_("lua::getWeaponSubskillType :: Invalid slot specified!");
                    return Ret(l, 0);
                }
                if (Entity(l) is not CharData c) return Ret(l, 0);
                var weapon = CharStats.WeaponIn(c, slot);
                return Ret(l, weapon?.Equip != null ? (byte)weapon.SubSkill : 0);
            };
            m["isWeaponTwoHanded"] = l => Ret(l, Battle(l) is { } b && Melee.MainTwoHanded(b));
            m["isDualWielding"] = l =>
            {
                if (Entity(l) is BattleEntity b) return Ret(l, Melee.IsDualWielding(b));
                log_("lua::isDualWielding :: NPCs don't wield weapons!");
                return Ret(l, false);
            };
            m["getEquipID"] = l =>
            {
                if (Entity(l) is not CharData c) return Ret(l, 0);
                int slot = l.ToEnumArg(2); // SLOTTYPE
                if (slot > 15)
                {
                    log_("Invalid slot passed to function.");
                    return Ret(l, 0);
                }
                return Ret(l, c.Equipped[slot]?.ItemId ?? 0);
            };

            // merits, job points (hasTrait is ZoneHost.Traits)
            m["getMerit"] = l => Ret(l, Entity(l) is CharData c ? c.Merits?.Value(c, (ushort)l.ToIntArg(2)) ?? 0 : 0);
            m["getJobPointLevel"] = l => Ret(l, Entity(l) is CharData c ? JobPointUtil.Value(c, l.ToIntArg(2)) : 0);
            // CJobPoints::GetJobPointsSpent for the main job, 0 below 99 (level sync)
            m["getSpentJobPoints"] = l => Ret(l, Entity(l) is CharData c && c.MLvl >= 99 ? c.JobPoints[c.MJob].Spent : 0);
            m["getWeather"] = l =>
            {
                object e = Entity(l);
                ushort zoneWeather = AreaOf(e)?.Weather ?? WeatherNone;
                if (e is not BattleEntity b) return Ret(l, zoneWeather);
                bool ignoreScholar = l.Type(2) == 1 && l.ToBoolean(2); // LUA_TBOOLEAN: sol2 takes only a boolean;
                return Ret(l, Weather(b, ignoreScholar, zoneWeather));
            };

            // enmity, MP and TP
            m["addEnmity"] = l =>
            {
                object self = Entity(l);
                var target = l.ToObject(2);
                int ce = l.ToIntArg(3), ve = l.ToIntArg(4);
                if (self is CharData c)
                {
                    if (target is MobEntity tm) tm.Enmity.UpdateEnmity(c, ce, ve);
                }
                else if (self is MobEntity sm)
                {
                    if (target is BattleEntity tb && (ce > 0 || ve > 0)) sm.Enmity.UpdateEnmity(tb, ce, ve);
                }
                return 0;
            };
            m["delMP"] = l => Ret(l, Battle(l) is { } b ? BattleAddMp(b, -l.ToIntArg(2)) : 0);
            m["delTP"] = l =>
            {
                if (Battle(l) is not { } b) return Ret(l, 0);
                short amount = (short)-l.ToIntArg(2);
                if (b is CharData c) return Ret(l, AddTp(c, amount));
                var mob = (MobEntity)b;
                int before = mob.Tp;
                AddMobTp(mob, amount);
                return Ret(l, Math.Abs(before - mob.Tp));
            };
            m["actionQueueEmpty"] = l => Ret(l, !actionQueues_.TryGetValue(Entity(l), out var queue) || queue.IsEmpty);

            m["getSubJob"] = l => Ret(l, Battle(l) is { } b ? b.SJob : 0);
            m["checkLiementAbsorb"] = l => Ret(l, Entity(l) is BattleEntity b ? LiementAbsorb(b, l.ToEnumArg(2)) : 1.0f);
            m["getEquippedItem"] = l =>
            {
                Item item = null;
                if (Entity(l) is CharData c)
                {
                    int slot = l.ToIntArg(2);
                    if (slot > LinkshellItem.SlotLink2) log_("Invalid slot passed to function.");
                    else item = slot >= LinkshellItem.SlotLink1 ? LinkshellItem.Equipped(c, slot) : CharStats.EquippedItem(c, slot);
                }
                l.PushObject(item, ItemType);
                return 1;
            };

            // the damage-taken helpers the damage scripts call between the formula and takeDamage
            m["handleSevereDamage"] = l =>
            {
                if (Entity(l) is BattleEntity b) return Ret(l, HandleSevereDamage(b, l.ToIntArg(2), l.ToBoolean(3)));
                log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                return Ret(l, 0);
            };
            m["checkDamageCap"] = l =>
            {
                if (Entity(l) is BattleEntity b) return Ret(l, MeleeFor().DamageCap(b, l.ToIntArg(2)));
                log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                return Ret(l, 0);
            };
            m["handleAfflatusMiseryDamage"] = l =>
            {
                // HandleAfflatusMiseryDamage: the pool Afflatus Misery's next Banish / Auspice reads
                int damage = (int)l.ToNumberArg(2);
                if (Battle(l) is { } b && damage > 0 && b.StatusEffects.HasStatusEffect(EffectAfflatusMisery))
                    b.SetModifier(Mod.AFFLATUS_MISERY, (short)damage);
                return 0;
            };
            m["updateEnmityFromDamage"] = l =>
            {
                object self = Entity(l);
                var target = l.ToObject(2);
                int damage = l.ToIntArg(3);
                if (target != null && IdOf(self) == IdOf(target))
                {
                    log_($"updateEnmityFromDamage(): Attempting to add enmity from damage to self ({NameOf(target)}, {IdOf(target)})!");
                    return 0;
                }
                if (self is CharData c)
                {
                    if (target is MobEntity tm) tm.Enmity.UpdateEnmityFromAttack(c, damage);
                }
                else if (self is MobEntity sm && target is BattleEntity tb && damage > 0)
                {
                    sm.Enmity.UpdateEnmityFromDamage(tb, damage);
                }
                return 0;
            };
            m["trySkillUp"] = l =>
            {
                if (Entity(l) is not CharData c) return 0;
                bool forceSkillUp = !l.IsNil(4) && l.ToBoolean(4);
                bool useSubSkill = !l.IsNil(5) && l.ToBoolean(4); // C++ reads the force argument for both
                TrySkillUp(c, l.ToIntArg(2), l.ToIntArg(3), forceSkillUp, useSubSkill);
                return 0;
            };

            // the main weapon's item level stats
            m["getILvlMacc"] = l => Ret(l, Battle(l) is CharData c ? c.MainWeapon?.IlvlMacc ?? 0 : 0);
            m["getILvlParry"] = l => Ret(l, Battle(l) is CharData c ? c.MainWeapon?.IlvlParry ?? 0 : 0);
            m["getILvlSkill"] = l => Ret(l, Battle(l) is CharData c ? c.MainWeapon?.IlvlSkill ?? 0 : 0);

            // a char bare handed or with an H2H weapon in its main slot; a mob with an H2H main weapon
            m["isUsingH2H"] = l => Ret(l, Entity(l) switch
            {
                CharData c => c.Equipped[CharStats.SlotMain] == null || (c.MainWeapon?.HandToHand ?? false),
                MobEntity mob => mob.WeaponSkill == Skill.HandToHand,
                _ => false,
            });
            m["getFacingAngle"] = l =>
                Ret(l, FacingAngle(PosOf(Entity(l)), PosOf(l.ToObject(2) ?? throw new InvalidOperationException("bad target: expected " + BaseEntityType))));
            m["getShieldSize"] = l =>
            {
                if (Entity(l) is CharData c)
                    return Ret(l, c.Equipped[CharStats.SlotSub] is { IsShield: true } shield ? shield.ShieldSize : 0);
                log_($"Entity is not a Player or Trust ({NameOf(Entity(l))}).");
                return Ret(l, 0);
            };
            m["getSpeed"] = l => Ret(l, Entity(l) switch
            {
                BattleEntity b => b.Speed,
                NpcEntity npc => npc.Speed,
                _ => 0,
            });
        }

        private const ushort EffectAfflatusMisery = 418, EffectMigawari = 471, EffectEarthManeuver = 303;

        // battleutils::HandleSevereDamage: Migawari takes a hit over its threshold of max HP (and goes; the reduction's
        // integer division leaves only all or nothing, as C++ does); a pet with AUTO_SCHURZEN whose master holds an Earth
        // Maneuver survives a fatal physical blow at 1 HP, the maneuver spent
        private int HandleSevereDamage(BattleEntity b, int damage, bool isPhysical = true)
        {
            if (b.StatusEffects.GetStatusEffect(EffectMigawari) is { } effect)
            {
                float damageThreshold = b.MaxHp * (effect.Power / 100.00f);
                if (damage > damageThreshold)
                {
                    int severeReduction = Math.Clamp(100 - effect.SubPower, 0, 100) / 100;
                    damage *= severeReduction;
                    b.StatusEffects.DelStatusEffect(EffectMigawari);
                }
            }
            if (isPhysical && b is PetEntity && b.GetMod(Mod.AUTO_SCHURZEN) != 0 && damage >= b.Hp && b.PMaster is { } master &&
                master.StatusEffects.GetEffectsCount(EffectEarthManeuver) >= 1)
            {
                damage = b.Hp - 1;
                master.StatusEffects.DelStatusEffectSilent(EffectEarthManeuver);
            }
            return damage;
        }

        // battleutils::CheckLiementAbsorb: Liement packs up to four damage types a nibble each in its subpower; a match
        // ends it and absorbs (85 + matches * power)%
        private static float LiementAbsorb(BattleEntity b, int damageType)
        {
            if (b.StatusEffects.GetStatusEffect(EffectLiement, 0) is not { } liement) return 1.0f;
            int matches = 0;
            for (int i = 0; i < 4; i++)
                if ((liement.SubPower >> i * 4 & 0xF) == damageType) matches++;
            if (matches == 0) return 1.0f;
            b.StatusEffects.DelStatusEffectSilent(EffectLiement);
            return (float)((85 + matches * (ushort)liement.Power) / 100.0) * -1;
        }

        private const ushort EffectLiement = 537;

        // CLuaBaseEntity::getStat: the stats, ATT (by weapon slot), ACC (by attack number), the ranged pair, DEF and EVA
        private int GetStat(BattleEntity b, Mod stat, LuaState l)
        {
            switch (stat)
            {
                case Mod.STR: return Melee.Str(b);
                case Mod.DEX: return Melee.Dex(b);
                case Mod.VIT: return Melee.Vit(b);
                case Mod.AGI: return Melee.Agi(b);
                case Mod.INT: return Melee.Int(b);
                case Mod.MND: return Melee.Mnd(b);
                case Mod.CHR: return Melee.Chr(b);
                case Mod.ATT: return (ushort)MeleeFor().Att(b, l.IsNumber(3) ? (int)l.ToEnum(3) : CharStats.SlotMain); // as<SLOTTYPE>
                case Mod.ACC: return (ushort)MeleeFor().Acc(b, l.IsNumber(3) ? (byte)l.ToIntegral(3) : 0);
                case Mod.RATT: return Ratt(b);
                case Mod.RACC: return Racc(b);
                case Mod.DEF: return (ushort)MeleeFor().Def(b);
                case Mod.EVA: return Melee.Eva(b);
                default: return 0;
            }
        }

        // CBattleEntity::RATT: Weakness at power 2 or more takes it away; a mob's or a pet's is its RATT mod and half its
        // STR, an automaton's adds its automaton ranged skill, a trust's its best ranged skill and 0.75 of its STR; plus
        // any bonus attack
        private int Ratt(BattleEntity b, int bonusAtt = 0)
        {
            if (b.StatusEffects.GetStatusEffect(EffectId.Weakness) is { Power: >= 2 }) return 0;
            if (b is CharData c) return (ushort)loader_.Stats.Ratt(c, bonusAtt);
            int skillLevel = 0;
            double strMultiplier = 0.5;
            if (b is PetEntity { PetType: PetType.Automaton }) skillLevel = Melee.GetSkill(b, SkillAutomatonRanged);
            else if (b is TrustEntity)
            {
                strMultiplier = 0.75;
                skillLevel = BestRangedSkill(b);
            }
            int ratt = 8 + skillLevel + bonusAtt + b.GetMod(Mod.RATT) + (int)Math.Floor(Melee.Str(b) * strMultiplier);
            float total = ratt + ratt * b.GetMod(Mod.RATTP) / 100f + Math.Min((short)(ratt * b.GetMod(Mod.FOOD_RATTP) / 100f), b.GetMod(Mod.FOOD_RATT_CAP));
            return (ushort)Math.Max((short)1, (short)total);
        }

        // CBattleEntity::RACC: Weakness at power 2 or more leaves 1. An automaton's comes from its automaton ranged skill,
        // half its AGI and its ACC (not RACC) mod; a trust's from its best ranged skill, 0.75 of its AGI and its RACC mod;
        // a mob's or a pet's is its RACC mod, its master's Tandem Strike when both are on the same target and half its
        // AGI (a pet's ecosystem correlation reads utils.getEcosystemStrengthBonus, which no script defines)
        private int Racc(BattleEntity b, int bonusAcc = 0)
        {
            if (b.StatusEffects.GetStatusEffect(EffectId.Weakness) is { Power: >= 2 }) return 1;
            if (b is CharData c) return (ushort)loader_.Stats.Racc(c, bonusAcc);
            int racc;
            if (b is PetEntity { PetType: PetType.Automaton })
            {
                racc = CharStats.AccFromSkill(Melee.GetSkill(b, SkillAutomatonRanged));
                racc += (int)Math.Floor(Melee.Agi(b) * 0.5);
                racc += b.GetMod(Mod.ACC) + bonusAcc;
            }
            else if (b is TrustEntity)
            {
                racc = CharStats.AccFromSkill(BestRangedSkill(b));
                racc += (int)Math.Floor(Melee.Agi(b) * 0.75);
                racc += b.GetMod(Mod.RACC) + bonusAcc;
            }
            else
            {
                racc = b.GetMod(Mod.RACC) + bonusAcc;
                if (Melee.IsTandemActive(b) && b.PMaster is CharData master) racc += master.GetMod(Mod.TANDEM_STRIKE_POWER);
                racc += Melee.Agi(b) / 2;
            }
            return (ushort)Math.Max(1, racc + Math.Min((short)((100 + b.GetMod(Mod.FOOD_RACCP) * racc) / 100), b.GetMod(Mod.FOOD_RACC_CAP)));
        }

        // a trust's archery, marksmanship or throwing skill, the highest
        private static int BestRangedSkill(BattleEntity b) =>
            Math.Max(Melee.GetSkill(b, Skill.Archery), Math.Max(Melee.GetSkill(b, Skill.Marksmanship), Melee.GetSkill(b, Skill.Throwing)));

        // CBattleEntity::GetSubWeaponRank
        private static int SubWeaponRank(BattleEntity b)
        {
            int dmg = Melee.SubWeaponDmg(b);
            if (b is CharData c && c.SubWeapon?.Equip is EquipmentInfo equip)
            {
                dmg += equip.GetModifier(Mod.MAIN_DMG_RANK);
                dmg -= equip.GetModifier(Mod.DMG_RATING);
            }
            return (ushort)dmg / 9;
        }

        // CBattleEntity::GetRangedWeaponDmg: a mob's ranged weapon is scaled like its melee one; a char's adds the ranged
        // weapon and the ammo, each scaled down while above the char's level
        private static int RangedWeaponDmg(BattleEntity b)
        {
            // ranged monster moves such as Sharp Sting hit as hard as the Monipulator's melee
            if (b is CharData { Monstrosity: { } mon } mc) return Monstrosity.BaseDamage(mon, mc.GetMod(Mod.RANGED_DMG_RATING));
            if (b is MobEntity m)
            {
                int baseDamageModifier = m.GetMobMod(MobMod.BaseDamageModifier);
                float damageMultiplier = m.DamageMultiplier / 100.0f;
                if (m.GetMobMod(MobMod.BaseDamageMultiplier) != 0) damageMultiplier = m.GetMobMod(MobMod.BaseDamageMultiplier) / 100.0f;
                int damage = (int)MathF.Floor((m.RangedDamage + baseDamageModifier) * damageMultiplier);
                damage += m.GetMod(Mod.RANGED_DMG_RATING) + m.GetMobMod(MobMod.RangedDamageOffset);
                return Math.Clamp(damage, 1, 65535);
            }
            var c = (CharData)b;
            int dmg = 0;
            foreach (var weapon in new[] { c.RangedWeapon, c.AmmoWeapon })
            {
                if (weapon == null) continue;
                dmg += ScaledDamage(c, weapon) + (weapon.Equip?.GetModifier(Mod.DMG_RATING) ?? 0);
            }
            return (ushort)(dmg + c.GetMod(Mod.RANGED_DMG_RATING));
        }

        // CBattleEntity::GetRangedWeaponRank: the ranged weapon (or the ammo when throwing) alone
        private static int RangedWeaponRank(BattleEntity b)
        {
            if (b is MobEntity m) return m.RangedDamage / 9;
            var c = (CharData)b;
            var weapon = c.RangedWeapon ?? c.AmmoWeapon;
            if (weapon == null) return 0;
            int dmg = (ushort)(ScaledDamage(c, weapon) + (weapon.Equip?.GetModifier(Mod.RANGED_DMG_RANK) ?? 0));
            return dmg / 9;
        }

        // a weapon's damage, scaled by level * 3 / 4 / its required level when that is above the char's
        private static int ScaledDamage(CharData c, WeaponInfo weapon)
        {
            int reqLvl = weapon.Equip?.ReqLvl ?? 0;
            if (reqLvl <= c.MLvl) return (int)weapon.Damage;
            ushort dmg = (ushort)weapon.Damage;
            dmg = (ushort)(dmg * (c.MLvl * 3));
            dmg /= 4;
            return (ushort)(dmg / reqLvl);
        }

        // battleutils::GetBaseRangedDelay: a throwing weapon alone, a bow or gun with its ammo, or thrown ammo; a mob's
        // reads its main weapon (as C++ does)
        private static int BaseRangedDelay(BattleEntity b)
        {
            if (b is MobEntity m) return m.BaseDelay;
            var c = (CharData)b;
            var ranged = c.RangedWeapon;
            var ammo = c.AmmoWeapon;
            if (ranged != null && ranged.Ranged)
            {
                if (ranged.Skill == Skill.Throwing) return (int)ranged.Delay;
                if (ammo != null) return (int)(ranged.Delay + ammo.Delay);
                return 0;
            }
            if (ammo != null && ammo.Ranged) return (int)ammo.Delay;
            return 0;
        }

        private static float HitboxSize(BattleEntity b) => b is MobEntity m ? m.ModelHitboxSize : ((CharData)b).ModelHitboxSize10 / 10f;

        // facingAngle(A, B): how far B lies from A's rotation, -128..128
        private static int FacingAngle((float X, float Y, float Z, byte Rot) a, (float X, float Y, float Z, byte Rot) b)
        {
            int diff = WorldAngle(a.X, a.Z, a.Rot, b.X, b.Z) - a.Rot;
            if (diff > 128) diff -= 256;
            else if (diff < -128) diff += 256;
            return diff;
        }

        // isFacing / isInfront / isBehind / isBeside: self, the target and half the cone (an 8-bit angle, 64 by default)
        private static bool ConeCheck(LuaState l, Func<(float X, float Y, float Z, byte Rot), (float X, float Y, float Z, byte Rot), int, bool> test)
        {
            var self = PosOf(Entity(l));
            var target = PosOf(l.ToObject(2) ?? throw new InvalidOperationException("bad target: expected " + BaseEntityType));
            byte cone = l.IsNil(3) ? (byte)64 : (byte)l.ToIntArg(3);
            return test(self, target, cone / 2);
        }

        // battleutils::GetWeather: a scholar storm on the entity gives its weak weather, and doubles the zone's when they
        // match; strong zone weather of the storm's element wins
        private static ushort Weather(BattleEntity b, bool ignoreScholar, ushort zoneWeather)
        {
            ushort scholarSpell = WeatherNone;
            if (!ignoreScholar)
            {
                var effects = b.StatusEffects;
                if (effects.HasStatusEffect(EffectFirestorm)) scholarSpell = WeatherHotSpell;
                if (effects.HasStatusEffect(EffectRainstorm)) scholarSpell = WeatherRain;
                if (effects.HasStatusEffect(EffectSandstorm)) scholarSpell = WeatherDustStorm;
                if (effects.HasStatusEffect(EffectWindstorm)) scholarSpell = WeatherWind;
                if (effects.HasStatusEffect(EffectHailstorm)) scholarSpell = WeatherSnow;
                if (effects.HasStatusEffect(EffectThunderstorm)) scholarSpell = WeatherThunder;
                if (effects.HasStatusEffect(EffectAurorastorm)) scholarSpell = WeatherAuroras;
                if (effects.HasStatusEffect(EffectVoidstorm)) scholarSpell = WeatherGloom;
            }
            if (ignoreScholar || scholarSpell == WeatherNone || zoneWeather == scholarSpell + 1) return zoneWeather;
            if (scholarSpell == zoneWeather) return (ushort)(zoneWeather + 1);
            return scholarSpell;
        }
    }
}
