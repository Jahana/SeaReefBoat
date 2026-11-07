using System;
using System.Collections.Generic;

namespace XI.Zone
{
    // The melee auto-attack math, ported natively from the C++ and Lua:
    // - battleutils::GetHitRate, GetCritHitRate, GetDamageRatio and GetFSTR, with the Lua they call
    //   (physical_hit_rate.lua and physical_utilities.lua);
    // - CAttackRound / CAttack, including ProcessDamage on the native path (map.ENABLE_AUTO_ATTACK_LUA = false);
    // - battleutils::TakePhysicalDamage for a melee swing;
    // - tp.lua and CBattleEntity::GetWeaponDelay;
    // - Sneak / Trick Attack: GetHitRateEx's sure hit, GetCritHitRate's sure crit, ProcessDamage's DEX / AGI bonus and the
    //   AUGMENTS_SA / AUGMENTS_TA multipliers, the round's SATA flag (CAttackRound::SetSATA) and its Trick Attack partner
    //   (battleutils::getAvailableTrickAttackChar, through TrickAttackCharOf).
    // - the merits on the round (Double / Triple Attack with the trait, Ambush, a MNK's Kick Attack), on the crit rate
    //   (Critical Hit Rate, Enemy Critical Hit Rate) and Store TP's; Innin / Yonin; Enlight's and Tandem Strike's
    //   accuracy (petutils::IsTandemActive).
    // - a Zanshin swing (AttackType.Zanshin, added by the attack loops): +35 accuracy, ZANSHIN_DOUBLE_DAMAGE, and
    //   Ikishoten's TP when the round could take one.
    // - the swing's damage: Consume Mana's base bonus, Scarlet Delirium's bonus, Soul Eater, a normal swing under a
    //   samba counting as a samba one (SAMBA_DOUBLE_DAMAGE); the damage taken's auto equalizer, HandleSevereDamage
    //   (Migawari, AUTO_SCHURZEN, through SevereDamage) and Fan Dance.
    // - the round's Mikage swings (one a shadow, the main weapon's), a Daken throw (CreateDakenAttack: the ammo slot, the
    //   throw animation, the ranged hit rate, damage ratio and base damage through the ZoneHost's RangedHitRateOf /
    //   RangedDamageRatioOf / RangedBaseDamageOf, Sange's merits) and the follow-up swings (ProcFollowUpAttacks:
    //   AMMO_SWING spending a virtue stone through RemoveAmmoOf); the damage taken of an ammo-slot swing as ranged.
    // - Restraint: each round's first landed swing builds its weapon skill damage bonus (ALL_WSDMG_FIRST_HIT, to +30 %,
    //   by the weapon delay, ENHANCES_RESTRAINT and the job points).
    // (The reactions themselves are in ZoneHost.Defense: parry, guard, block, shadows, counters, anticipation.) The pets' ecosystem correlation reads
    // utils.getEcosystemStrengthBonus, which no script defines (so it never applies in C++ either).
    public sealed class Melee
    {
        public const int SlotMain = CharStats.SlotMain, SlotSub = CharStats.SlotSub;

        // PHYSICAL_ATTACK_TYPE and PHYSICAL_ATTACK_DIRECTION, as far as a melee round goes
        public enum AttackType : byte { Normal, Double, Triple, Quad, Kick, Zanshin, Samba, Daken, FollowUp }
        public enum Direction : byte { Right, Left }

        public sealed class Swing
        {
            public AttackType Type;
            public Direction Direction;
            public bool First;
            public Round Round;
            public bool SneakAttack, TrickAttack; // CAttack m_isSA / m_isTA, set by ProcessDamage
            public bool Covered;                  // CAttack m_isCovered, set by CheckCover
            public bool Critical;                 // CAttack m_isCritical, as the swing's listeners see it (CLuaAttack)
        }

        // CAttackRound's SATA flag (a sure-hit right swing: the round hits and skips shadows), its Trick Attack partner
        // and its cover ability user (a char target's), taken as the round is built, with the target it was built on
        public sealed class Round
        {
            public bool Sata;
            public BattleEntity TaEntity, CoverUser, OriginalTarget;
        }

        private const byte JobThf = 6, JobDrk = 8;
        private const int TraitAssassin = 68;
        private const ushort EffectHide = 76, EffectDoubt = 576, EffectHasteSambaHaste = 796, EffectInvincible = 50,
            EffectSentinel = 62, EffectReprisal = 403, EffectEnlight = 274, EffectEndark = 288, EffectYonin = 420, EffectInnin = 421,
            EffectFormlessStrikes = 341, EffectAfflatusMisery = 418, EffectScarletDelirium = 479, EffectScarletDelirium1 = 480,
            EffectSouleater = 63, EffectConsumeMana = 599, EffectFanDance = 411, EffectDrainSamba = 368, EffectAspirSamba = 369,
            EffectHasteSamba = 370, EffectSange = 352, EffectMikage = 502, EffectRestraint = 435, EffectMeikyoShisui = 54;
        private const int JpRestraintEffect = 0x020 + 0x07; // JP_RESTRAINT_EFFECT
        private const ushort MeritSange = 2816, VirtueStone = 18244;
        private const sbyte SubSkillShuriken = 3;
        private const int TraitDoubleAttack = 15, TraitTripleAttack = 16, TraitAmbush = 76;
        private const ushort MeritCritHitRate = 324, MeritEnemyCritRate = 326, MeritDoubleAttackRate = 392, MeritKickAttackRate = 456,
            MeritTripleAttackRate = 712, MeritStoreTpEffect = 1092, MeritSubtleBlowEffect = 1152, MeritAmbush = 2374, MeritIkishoten = 2756, MeritFormlessStrikes = 2114;
        private const int SkillAutomatonMelee = 22;
        public const int WorldAngleMaxDeviance = 8;
        public const float WorldAngleMinDistance = 0.5f;

        // battleutils::getAvailableTrickAttackChar(taUser, mob): the ZoneHost's alliance lookup (none without one)
        public Func<BattleEntity, BattleEntity, BattleEntity> TrickAttackCharOf = (_, _) => null;
        // battleutils::GetCoverAbilityUser(coverTarget, attacker): the ZoneHost's party lookup
        public Func<BattleEntity, BattleEntity, BattleEntity> CoverAbilityUserOf = (_, _) => null;
        // battleutils::HandleSevereDamage(defender, damage, isPhysical): the ZoneHost's Migawari / AUTO_SCHURZEN
        public Func<BattleEntity, int, bool, int> SevereDamage = (_, damage, _) => damage;
        // CBattleEntity::addHP for a char (Soul Eater's cost): the ZoneHost's
        public Func<CharData, int, int> AddCharHp = (_, _) => 0;
        // a Daken throw's ranged battleutils: GetRangedHitRate(accBonus), GetRangedDamageRatio(bonusRangedAttack) and
        // GetRangedWeaponDmg + calculateRangedStatFactor (the ammo slot's fSTR)
        public Func<BattleEntity, BattleEntity, int, int> RangedHitRateOf = (_, _, _) => 0;
        public Func<BattleEntity, BattleEntity, bool, int, float> RangedDamageRatioOf = (_, _, _, _) => 1.0f;
        public Func<BattleEntity, BattleEntity, int> RangedBaseDamageOf = (_, _) => 0;
        // battleutils::RemoveAmmo (a follow-up's virtue stone)
        public Func<CharData, int, bool> RemoveAmmoOf = (_, _) => false;
        // xi.combat.physicalHitRate.getHitRateModifiers(attacker, target, false, false): the ZoneHost's accuracy and
        // evasion bonuses from facing, effects, merits and job points
        public Func<BattleEntity, BattleEntity, (int Acc, int Eva)> HitRateModifiersOf = (_, _) => (0, 0);
        // shouldApplySignetBonus(attacker, target) of physical_hit_rate.lua / physical_utilities.lua: the ZoneHost's
        public Func<BattleEntity, BattleEntity, bool> SignetBonusOf = (_, _) => false;

        private const int MaxAttacks = 8;

        private readonly CharStats stats_;
        private readonly StatSettings cfg_;
        private readonly Func<Random> rng_;

        public Melee(CharStats stats, StatSettings cfg, Func<Random> rng)
        {
            stats_ = stats;
            cfg_ = cfg;
            rng_ = rng;
        }

        private Random Rng => rng_();

        // xirand::GetRandomNumber(max): [0, max)
        private int Roll(int max) => max <= 0 ? 0 : Rng.Next(max);

        // math.randomInt: [lower, upper], the bounds rounded to the nearest integer
        private long RandomInt(double lower, double upper)
        {
            long lo = (long)Math.Round(lower, MidpointRounding.AwayFromZero), hi = (long)Math.Round(upper, MidpointRounding.AwayFromZero);
            return hi <= lo ? lo : Rng.NextInt64(lo, hi + 1);
        }

        // ---- the entity getters -------------------------------------------------------------------------------------

        private static bool IsPc(BattleEntity e) => e is CharData;

        public static byte MainSkill(BattleEntity e) => e switch
        {
            CharData c => c.MainWeapon?.Skill ?? Skill.None,
            MobEntity m => m.WeaponSkill,
            _ => Skill.None,
        };

        public static bool MainTwoHanded(BattleEntity e) => e switch
        {
            CharData c => c.MainWeapon != null && c.MainWeapon.TwoHanded,
            MobEntity m => new WeaponInfo { Skill = m.WeaponSkill }.TwoHanded,
            _ => false,
        };

        // CAttackRound::IsH2H: the main weapon's skill
        public static bool IsH2H(BattleEntity e) => MainSkill(e) == Skill.HandToHand;

        public static bool IsDualWielding(BattleEntity e) => e switch
        {
            CharData c => c.DualWield,
            MobEntity m => m.IsDualWielding,
            _ => false,
        };

        public static int Str(BattleEntity e)
        {
            if (e is CharData c) return CharStats.Str(c);
            var m = (MobEntity)e;
            int bonus = MainTwoHanded(m) ? m.GetMod(Mod.TWOHAND_STR) : 0;
            return Math.Clamp(m.Stats[0] + m.GetMod(Mod.STR) + bonus, 0, 999);
        }

        public static int Dex(BattleEntity e) =>
            e is CharData c ? CharStats.Dex(c) : Math.Clamp(((MobEntity)e).Stats[1] + e.GetMod(Mod.DEX), 0, 999);

        public static int Vit(BattleEntity e) =>
            e is CharData c ? CharStats.Vit(c) : Math.Clamp(((MobEntity)e).Stats[2] + e.GetMod(Mod.VIT), 0, 999);

        public static int Agi(BattleEntity e) =>
            e is CharData c ? CharStats.Agi(c) : Math.Clamp(((MobEntity)e).Stats[3] + e.GetMod(Mod.AGI), 0, 999);

        // CBattleEntity::INT / MND / CHR: stats.X + its mod
        public static int Int(BattleEntity e) => Math.Clamp(BaseStat(e, 4) + e.GetMod(Mod.INT), 0, 999);
        public static int Mnd(BattleEntity e) => Math.Clamp(BaseStat(e, 5) + e.GetMod(Mod.MND), 0, 999);
        public static int Chr(BattleEntity e) => Math.Clamp(BaseStat(e, 6) + e.GetMod(Mod.CHR), 0, 999);

        private static int BaseStat(BattleEntity e, int index) => e is CharData c ? c.BaseStats[index] : ((MobEntity)e).Stats[index];

        // CBattleEntity::ATT: a mob has its ATT mod, half its STR and Endark's potency, an automaton its automaton melee
        // skill too. A pet's ecosystem correlation reads utils.getEcosystemStrengthBonus, which no script defines, so
        // it never applies (in C++ either).
        public int Att(BattleEntity e, int slot)
        {
            if (e is CharData c) return stats_.Att(c, slot);
            int att = 8 + e.GetMod(Mod.ATT);
            int attp = e.GetMod(Mod.ATTP);
            att = (int)(att + Str(e) * 0.5f);
            if (e.StatusEffects.HasStatusEffect(EffectEndark)) att += e.GetMod(Mod.ENSPELL_DMG);
            if (e is PetEntity { PetType: PetType.Automaton }) att += GetSkill(e, SkillAutomatonMelee);
            return Math.Max(1, att + att * attp / 100 + Math.Min((short)(att * e.GetMod(Mod.FOOD_ATTP) / 100), e.GetMod(Mod.FOOD_ATT_CAP)));
        }

        // CBattleEntity::ACC: an automaton's from its automaton melee skill and half its DEX, a mob's its ACC mod and half
        // its DEX; either with Enlight's potency and, a char's pet on its master's target, the master's Tandem Strike
        public int Acc(BattleEntity e, int attackNumber, int offsetAccuracy = 0)
        {
            if (e is CharData c) return stats_.Acc(c, attackNumber, offsetAccuracy);
            int acc;
            if (e is PetEntity { PetType: PetType.Automaton })
            {
                acc = CharStats.AccFromSkill(GetSkill(e, SkillAutomatonMelee));
                acc += (int)MathF.Floor(Dex(e) * 0.5f);
                acc += e.GetMod(Mod.ACC) + offsetAccuracy;
                if (e.StatusEffects.HasStatusEffect(EffectEnlight)) acc += e.GetMod(Mod.ENSPELL_DMG);
                if (IsTandemActive(e) && e.PMaster is CharData am) acc += am.GetMod(Mod.TANDEM_STRIKE_POWER);
            }
            else
            {
                acc = e.GetMod(Mod.ACC) + offsetAccuracy;
                if (e.StatusEffects.HasStatusEffect(EffectEnlight)) acc += e.GetMod(Mod.ENSPELL_DMG);
                if (IsTandemActive(e) && e.PMaster is CharData mm) acc += mm.GetMod(Mod.TANDEM_STRIKE_POWER);
                acc += Dex(e) / 2;
            }
            return Math.Max(1, acc + Math.Min((short)((100 + e.GetMod(Mod.FOOD_ACCP) * acc) / 100), e.GetMod(Mod.FOOD_ACC_CAP)));
        }

        // petutils::IsTandemActive: a char and its pet (or a char's pet and its master) engaged on the same target
        public static bool IsTandemActive(BattleEntity e)
        {
            BattleEntity partner;
            if (e is CharData c)
            {
                if (c.PPet == null) return false;
                partner = c.PPet;
            }
            else
            {
                if (e.PMaster is not CharData) return false;
                partner = e.PMaster;
            }
            var target = BattleTargetOf(partner);
            return IsEngaged(partner) && target != null && target == BattleTargetOf(e);
        }

        private static BattleEntity BattleTargetOf(BattleEntity e) => e switch
        {
            CharData c => c.BattleTarget,
            MobEntity m => m.BattleTarget,
            _ => null,
        };

        private static bool IsEngaged(BattleEntity e) => e switch
        {
            CharData c => c.BattleTarget != null,
            MobEntity m => m.Engaged,
            _ => false,
        };

        // CBattleEntity::DEF: a mob's VIT counts half (an ally's, a pet's or a trust's, by
        // PLAYER_ALLIES_VIT_DEF_MULTIPLIER), and its level not at all
        public int Def(BattleEntity e)
        {
            if (e is CharData c) return stats_.Def(c);
            float vitFactor = IsMobObject(e) ? 0.5f : stats_.Settings.VitDefMultiplier;
            int def = 8 + (int)MathF.Floor(Vit(e) * vitFactor);
            def += e.GetMod(Mod.DEF);
            if (CharStats.CounterstanceDef(e, def, stats_.Settings) is int counterstance) return counterstance;
            return Math.Max(1, def + def * e.GetMod(Mod.DEFP) / 100 + Math.Min((short)(def * e.GetMod(Mod.FOOD_DEFP) / 100), e.GetMod(Mod.FOOD_DEF_CAP)));
        }

        // C++ objtype TYPE_MOB: a mob, a charmed one included (not a pet, a trust or a char)
        public static bool IsMobObject(BattleEntity e) => e is MobEntity && e is not PetEntity && e is not TrustEntity;

        // CBattleEntity::EVA: a mob and a pet other than an automaton have their EVA mod (no evasion skill); a trust
        // and an automaton their evasion skill (past 200 at 90 %) plus the EVA mod; all half their AGI
        public static int Eva(BattleEntity e)
        {
            if (e is CharData c) return CharStats.Eva(c);
            int evasion;
            if (IsMobObject(e) || e is PetEntity && e is not AutomatonEntity) evasion = e.GetMod(Mod.EVA);
            else
            {
                evasion = GetSkill(e, Skill.Evasion);
                if (evasion > 200) evasion = (int)(200 + (evasion - 200) * 0.9);
                evasion += e.GetMod(Mod.EVA);
            }
            evasion += Agi(e) / 2;
            evasion += (int)MathF.Floor((float)evasion * e.GetMod(Mod.EVA_PERCENT) / 100.0f);
            return Math.Clamp(evasion, 1, ushort.MaxValue);
        }

        public static int GetSkill(BattleEntity e, int skill) => e switch
        {
            CharData c => CharStats.GetSkill(c, skill),
            MobEntity m => skill < Skill.Max ? m.WorkingSkills[skill] : 0,
            _ => 0,
        };

        // CBattleEntity::GetMainWeaponDmg / GetSubWeaponDmg
        public static int MainWeaponDmg(BattleEntity e) => WeaponDmg(e, SlotMain);
        public static int SubWeaponDmg(BattleEntity e) => WeaponDmg(e, SlotSub);

        private static int WeaponDmg(BattleEntity e, int slot)
        {
            var rating = slot == SlotMain ? Mod.MAIN_DMG_RATING : Mod.SUB_DMG_RATING;
            if (e is MobEntity m)
            {
                int weaponDamage = slot == SlotMain ? m.MainDamage : m.SubDamage;
                int baseDamageModifier = m.GetMobMod(MobMod.BaseDamageModifier);
                float damageMultiplier = m.DamageMultiplier / 100.0f;
                if (m.GetMobMod(MobMod.BaseDamageMultiplier) != 0) damageMultiplier = m.GetMobMod(MobMod.BaseDamageMultiplier) / 100.0f;
                int damage = (int)MathF.Floor((weaponDamage + baseDamageModifier) * damageMultiplier);
                damage += m.GetMod(rating) + m.GetMobMod(MobMod.DamageOffset);
                return Math.Clamp(damage, 1, 65535);
            }
            var c = (CharData)e;
            if (c.Monstrosity != null && slot == SlotMain) return Monstrosity.BaseDamage(c.Monstrosity, c.GetMod(rating));
            var weapon = CharStats.WeaponIn(c, slot);
            if (weapon == null) return 0;
            // the unarmed items are bare CItemWeapons: CItemEquipment's default required level of 255 scales their 3
            // damage down to 0 at any level
            int reqLvl = weapon.Equip?.ReqLvl ?? 255;
            int itemRating = weapon.Equip?.GetModifier(Mod.DMG_RATING) ?? 0;
            if (reqLvl > c.MLvl)
            {
                ushort dmg = (ushort)weapon.Damage;
                dmg = (ushort)(dmg * (c.MLvl * 3));
                dmg /= 4;
                dmg = (ushort)(dmg / reqLvl);
                return (ushort)(dmg + itemRating + c.GetMod(rating));
            }
            return (ushort)(weapon.Damage + itemRating + c.GetMod(rating));
        }

        // CBattleEntity::GetMainWeaponRank
        public static int MainWeaponRank(BattleEntity e)
        {
            int dmg = MainWeaponDmg(e);
            if (e is CharData c && c.MainWeapon is WeaponInfo w)
            {
                dmg += w.Equip?.GetModifier(Mod.MAIN_DMG_RANK) ?? 0;
                dmg -= w.Equip?.GetModifier(Mod.DMG_RATING) ?? 0;
                if (w.HandToHand) dmg += 3;
            }
            return (ushort)dmg / 9;
        }

        // xi.data.levelCorrection.isLevelCorrectedZone
        public bool IsLevelCorrected(BattleEntity e) => !cfg_.UseAdoulinWeaponSkillChanges || LevelCorrectionZones.Contains(e.Zone);

        // ---- hit rate (GetHitRate -> xi.combat.physicalHitRate.getPhysicalHitRate) -----------------------------------

        // isUsingH2H as tp.lua and the hit-rate cap read it: an H2H main weapon, or nothing in the main slot
        private static bool UsingH2H(CharData c) => c.Equipped[SlotMain] == null || (c.MainWeapon?.HandToHand ?? false);

        // xi.combat.physicalHitRate.getPhysicalHitRateCap
        private static double HitRateCap(BattleEntity attacker, int slot)
        {
            if (attacker is PetEntity) return 0.99;
            if (attacker is CharData c)
            {
                if (UsingH2H(c)) return 0.99;
                if (MainTwoHanded(c) || slot >= 1) return 0.95;
                return 0.99;
            }
            return 0.95;
        }

        // behind(A, B, 64): A stands in the 64-wide cone behind B
        public static bool Behind(BattleEntity a, BattleEntity b) =>
            Math.Abs(AngleDifference(ZoneHost.WorldAngle(b.X, b.Z, b.Rotation, a.X, a.Z), b.Rotation)) > 128 - 64 / 2;

        // infront(B, A, 64): A stands in the 64-wide cone in front of B
        public static bool InFront(BattleEntity a, BattleEntity b) =>
            Math.Abs(AngleDifference(ZoneHost.WorldAngle(b.X, b.Z, b.Rotation, a.X, a.Z), b.Rotation)) < 64 / 2;

        // angleDifference: -128..128
        public static int AngleDifference(byte a, byte b)
        {
            int diff = a - b;
            if (diff > 128) diff -= 256;
            else if (diff < -128) diff += 256;
            return diff;
        }

        // battleutils::GetHitRateEx: an attack with SA active (from behind) or TA with Assassin (a partner in line) cannot
        // miss; else xi.combat.physicalHitRate.getPhysicalHitRate (not a weapon skill), floored to a percent
        public byte HitRate(BattleEntity attacker, BattleEntity defender, int attackNumber, int offsetAccuracy = 0)
        {
            bool hasSneakAttack = attacker.StatusEffects.HasStatusEffect(EffectId.SneakAttack);
            bool hasTrickAttack = attacker.StatusEffects.HasStatusEffect(EffectId.TrickAttack);
            bool hasAssassin = attacker is CharData ac && CharStats.HasTrait(ac, TraitAssassin);
            if (hasSneakAttack && Behind(attacker, defender) || hasTrickAttack && hasAssassin && TrickAttackCharOf(attacker, defender) != null)
                return 100;
            double cap = HitRateCap(attacker, attackNumber);
            // getPhysicalHitRate adds the bonus to getACC(slot), which has already floored the accuracy at 0
            double acc = Acc(attacker, attackNumber) + offsetAccuracy;
            double eva = Eva(defender);
            var (accBonus, evaBonus) = HitRateModifiersOf(attacker, defender);
            acc += accBonus;
            if (SignetBonusOf(attacker, defender)) eva = Math.Floor(eva * 1.25);
            eva += evaBonus;
            if (IsLevelCorrected(attacker))
            {
                int dlvl = attacker.MLvl - defender.MLvl;
                // an avatar's level difference caps at 38
                if (attacker is PetEntity av && PetId.IsAvatar(av.PetId)) dlvl = Math.Clamp(dlvl, 0, 38);
                if (!IsPc(attacker) && attacker.MLvl > defender.MLvl) acc += dlvl * 4;
                else if (IsPc(attacker) && attacker.MLvl < defender.MLvl) acc += dlvl * 4;
            }
            double hitrate = (75 + (acc - eva) / 2) / 100;
            hitrate = Math.Clamp(hitrate, 0.2, cap);
            return (byte)Math.Floor(hitrate * 100);
        }

        // ---- critical hits (GetCritHitRate, GetDexCritBonus) ---------------------------------------------------------

        private static int DexCritBonus(BattleEntity attacker, BattleEntity defender)
        {
            int dDex = Math.Clamp(Dex(attacker) - Agi(defender), 0, 50);
            int rate = dDex > 39 ? dDex - 35 : dDex > 29 ? 4 : dDex > 19 ? 3 : dDex > 13 ? 2 : dDex > 6 ? 1 : 0;
            return Math.Min(rate, 15);
        }

        // battleutils::GetCritHitRate. Sneak Attack (from behind, under Hide, or a THF on a Doubt target) or Trick Attack (a
        // THF with Assassin and a partner) is a sure crit unless ignored (the round's later swings); SA without its
        // condition leaves the flat 5.
        public byte CritHitRate(BattleEntity attacker, BattleEntity defender, int slot, bool ignoreSneakTrickAttack = false)
        {
            if (attacker.StatusEffects.HasStatusEffect(EffectId.MightyStrikes)) return 100;
            int rate = 5;
            if (attacker is CharData sa && !ignoreSneakTrickAttack && sa.StatusEffects.HasStatusEffect(EffectId.SneakAttack))
            {
                if (sa.MJob == JobThf && defender.StatusEffects.HasStatusEffect(EffectDoubt)) return 100;
                if (Behind(attacker, defender) || sa.StatusEffects.HasStatusEffect(EffectHide)) return 100;
                return (byte)rate;
            }
            if (attacker is CharData ta && ta.MJob == JobThf && CharStats.HasTrait(ta, TraitAssassin) && !ignoreSneakTrickAttack &&
                ta.StatusEffects.HasStatusEffect(EffectId.TrickAttack))
                return (byte)(TrickAttackCharOf(attacker, defender) != null ? 100 : rate);
            if (attacker is CharData c)
            {
                rate += c.Merits?.Value(c, MeritCritHitRate) ?? 0;
                var main = c.MainWeapon;
                var sub = c.Equipped[SlotSub];
                if (main != null && !main.TwoHanded && !main.HandToHand &&
                    (sub == null || (c.SubWeapon != null && c.SubWeapon.Skill == Skill.None) || sub.IsShield))
                    rate += c.GetMod(Mod.FENCER_CRITHITRATE);
            }
            if (defender is CharData dc) rate -= dc.Merits?.Value(dc, MeritEnemyCritRate) ?? 0;
            // Innin from behind the target; Yonin against an attacker in front
            if (attacker.StatusEffects.GetStatusEffect(EffectInnin) is { } innin && Behind(attacker, defender)) rate += innin.Power;
            if (defender.StatusEffects.GetStatusEffect(EffectYonin) is { } yonin && InFront(attacker, defender)) rate -= yonin.Power;
            rate += DexCritBonus(attacker, defender);
            rate += attacker.GetMod(Mod.CRITHITRATE);
            rate -= defender.GetMod(Mod.CRITICAL_HIT_EVASION);
            if (attacker is CharData pc && pc.Equipped[slot] is EquipmentInfo item && item.Weapon != null &&
                item.GetModifier(Mod.CRITHITRATE_ONLY_WEP) > 0)
                rate += item.GetModifier(Mod.CRITHITRATE_ONLY_WEP);
            return (byte)Math.Clamp(rate, 0, 100);
        }

        // ---- fSTR (xi.combat.physical.calculateMeleeStatFactor) -----------------------------------------------------

        public static int FStr(BattleEntity attacker, BattleEntity defender)
        {
            int mLvl = attacker.MLvl;
            if (attacker is MobEntity && mLvl <= 1) return 1;
            int statDiff = Str(attacker) - Vit(defender);
            double fStr;
            if (!IsPc(attacker))
            {
                fStr = statDiff >= 36 ? (statDiff - 4) / 4.0
                     : statDiff >= 26 ? (statDiff - 3) / 4.0
                     : statDiff >= 17 ? (statDiff - 2) / 4.0
                     : statDiff >= 4 ? (statDiff - 1) / 4.0
                     : statDiff >= -8 ? statDiff / 4.0
                     : statDiff >= -13 ? (statDiff + 1) / 4.0
                     : statDiff >= -19 ? (statDiff + 3) / 4.0
                     : statDiff >= -32 ? (statDiff + 4) / 4.0
                     : statDiff >= -42 ? (statDiff + 5) / 4.0
                     : statDiff >= -54 ? (statDiff + 6) / 4.0
                     : statDiff >= -67 ? (statDiff + 7) / 4.0
                     : statDiff >= -76 ? (statDiff + 8) / 4.0
                     : (statDiff + 9) / 4.0;
                fStr = Math.Floor(fStr);
                return (int)Math.Clamp(fStr, -1 - mLvl / 5, mLvl / 5 + 5);
            }
            int weaponRank = MainWeaponRank(attacker);
            statDiff = Math.Clamp(statDiff, (7 + weaponRank * 2) * -2, (14 + weaponRank * 2) * 2);
            int f = statDiff >= 12 ? statDiff + 4
                  : statDiff >= 6 ? statDiff + 6
                  : statDiff >= 1 ? statDiff + 7
                  : statDiff >= -2 ? statDiff + 8
                  : statDiff >= -7 ? statDiff + 9
                  : statDiff >= -15 ? statDiff + 10
                  : statDiff >= -21 ? statDiff + 12
                  : statDiff + 13;
            int upperCap = weaponRank + 8;
            int lowerCap = weaponRank == 0 ? -1 : -weaponRank;
            // callGlobal<int32>: sol2 takes the Lua number with llround (half away from zero)
            return (int)Math.Round(Math.Clamp(f / 4.0, lowerCap, upperCap), MidpointRounding.AwayFromZero);
        }

        // ---- pDIF (GetDamageRatio -> xi.combat.physical.calculateMeleePDIF) -----------------------------------------

        private static double PdifWeaponCap(byte skill) => skill switch
        {
            Skill.HandToHand => 3.5,
            Skill.Dagger or Skill.Sword or Skill.Axe or Skill.Katana or Skill.Club or Skill.Archery or Skill.Throwing => 3.25,
            Skill.GreatSword or Skill.GreatAxe or Skill.Polearm or Skill.Staff => 3.75,
            Skill.Scythe => 4,
            Skill.GreatKatana or Skill.Marksmanship => 3.5,
            _ => 3,
        };

        private static (double Lower, double Upper) WRatioCapPc(double w, double finalCap)
        {
            double upper = w < 0.5 ? w + 0.5 : w < 0.7 ? 1 : w < 1.2 ? w + 0.3 : w < 1.5 ? w + w * 0.25 : Math.Min(w + 0.375, finalCap);
            double lower = w < 0.38 ? 0 : w < 1.25 ? w * 1176 / 1024 - 448.0 / 1024 : w < 1.51 ? 1
                : w < 2.44 ? w * 1176 / 1024 - 775.0 / 1024 : Math.Min(w - 0.375, finalCap);
            return (lower, upper);
        }

        private static (double Lower, double Upper) WRatioCapOthers(double w, double finalCap)
        {
            double upper = w < 0.55 ? 0.6 + 760.0 / 1024 * w
                : w <= 0.8 ? 1
                : w < 1.2 ? 1 + 1127.0 / 1024 * (w - 0.8)
                : w < 1.5 ? 1474.0 / 1024 + 1105.0 / 1024 * (w - 1235.0 / 1024)
                : Math.Min(1803.0 / 1024 + 1070.0 / 1024 * (w - 1.5), finalCap);
            double lower = w <= 0.4 ? 0.25
                : w < 1.35 ? 0.25 + 827.0 / 1024 * (w - 0.4)
                : w <= 1.60 ? 1
                : Math.Min(1 + 1120.0 / 1024 * (w - 1.59), finalCap);
            return (lower, upper);
        }

        private static double SpikeRatio(bool isPc, double w)
        {
            if (isPc)
                return w > 0.5 && w < 1.5 ? Math.Clamp((0.5 - Math.Abs(w - 1)) * 1.2, 0, 1.0 / 3) : 0;
            double s = w > 0.0 && w < 0.75 ? -5.0 / 9 + 10.0 / 9 * w : w <= 1.3 ? 0.3 : 5.0 / 3 - 270.0 / 256 * w;
            return Math.Clamp(s, 0, 0.3);
        }

        // attackMod: GetDamageRatio's bonusAttPercent (calculateMeleePDIF's wsAttackMod), 1 for a plain swing
        public float DamageRatio(BattleEntity attacker, BattleEntity defender, bool isCritical, byte weaponType, int slot, float attackMod = 1.0f)
        {
            bool levelCorrected = IsLevelCorrected(attacker);
            double targetDefense = Math.Max(1, Def(defender));
            // getTargetDefense: Signet's DEF, 3.125 a level to 40 and 7.5 a level past it (at most 200)
            if (SignetBonusOf(attacker, defender))
            {
                int level = defender.MLvl;
                targetDefense += Math.Floor(Math.Min(level * 3.125, 125) + Math.Clamp((level - 40) * 7.5, 0, 75));
            }
            double actorAttack = Math.Floor(Math.Max(1, Att(attacker, slot)) * (double)attackMod);
            double baseRatio = actorAttack / targetDefense;

            double levelDifFactor = 0;
            if (levelCorrected) levelDifFactor = Math.Clamp(attacker.MLvl - defender.MLvl, -38, 38) * 3 / 64.0;
            bool isPc = IsPc(attacker);
            if (!isPc && levelDifFactor < 0) levelDifFactor = 0;
            if (isPc && levelDifFactor > 0) levelDifFactor = 0;

            double wRatio = baseRatio + (isCritical ? 1 : 0) + levelDifFactor;
            double damageLimitPlus = attacker.GetMod(Mod.DAMAGE_LIMIT) / 100.0;
            double damageLimitPercent = 1 + attacker.GetMod(Mod.DAMAGE_LIMITP) / 100.0;
            double lower, upper;
            if (isPc)
            {
                double finalCap = (PdifWeaponCap(weaponType) + damageLimitPlus) * damageLimitPercent + (isCritical ? 1 : 0) + levelDifFactor;
                if (RandomInt(1, 10000) / 10000.0 <= SpikeRatio(true, wRatio)) return (float)Math.Max(1.0 + levelDifFactor, 0);
                (lower, upper) = WRatioCapPc(wRatio, finalCap);
            }
            else
            {
                double basePdif = levelCorrected ? 2 : 4;
                double critBonus = levelCorrected && isCritical ? 1 : 0;
                double finalCap = (basePdif + damageLimitPlus) * damageLimitPercent + critBonus + levelDifFactor;
                if (RandomInt(1, 10000) / 10000.0 <= SpikeRatio(false, wRatio)) return (float)Math.Max(1.0 + levelDifFactor, 0);
                (lower, upper) = WRatioCapOthers(wRatio, finalCap);
            }

            double upperMax = RandomInt(0, 1) == 0 ? 0.5 : 0;
            double upperBound = Math.Max(upper, upperMax);
            double lowerBound = Math.Max(lower, 0);
            if (upperBound == 0) return 0;
            double pDif = RandomInt(lowerBound * 1000, upperBound * 1000) / 1000.0;
            pDif *= 1 + RandomInt(0, 5) * 0.01;
            if (isCritical)
            {
                int critDamageBonus = Math.Clamp(attacker.GetMod(Mod.CRIT_DMG_INCREASE) - defender.GetMod(Mod.CRIT_DEF_BONUS), 0, 100);
                pDif = pDif * (100 + critDamageBonus) / 100;
            }
            return Math.Max((float)pDif, 0f);
        }

        // ---- the attack round (CAttackRound) ------------------------------------------------------------------------

        // battleutils::getHitCount: the chance of each hit count up to the weapon's maximum
        private static readonly int[][] HitCountCdf =
        {
            null, null,
            new[] { 55 },
            new[] { 30, 80 },
            new[] { 20, 50, 80 },
            new[] { 10, 30, 60, 90 },
            new[] { 10, 30, 50, 70, 90 },
            new[] { 5, 20, 45, 70, 85, 95 },
            new[] { 5, 20, 45, 70, 85, 95, 98 },
        };

        public int HitCount(int hits)
        {
            int distribution = Roll(100);
            int num = 1;
            if (hits >= 2 && hits <= 8)
            {
                foreach (int bound in HitCountCdf[hits])
                {
                    if (distribution < bound) break;
                    num++;
                }
            }
            return Math.Min(num, 8);
        }

        public List<Swing> BuildRound(BattleEntity attacker)
        {
            var swings = new List<Swing>();
            bool h2h = IsH2H(attacker);
            // CAttackRound: grab a trick attack assistant
            BattleEntity battleTarget = attacker is CharData bc ? bc.BattleTarget : (attacker as MobEntity)?.BattleTarget;
            var round = new Round
            {
                TaEntity = battleTarget == null ? null : TrickAttackCharOf(attacker, battleTarget),
                // get cover partner
                CoverUser = battleTarget is CharData ? CoverAbilityUserOf(battleTarget, attacker) : null,
                OriginalTarget = battleTarget,
            };

            void Add(AttackType type, Direction direction, int count)
            {
                if (swings.Count >= MaxAttacks) return;
                for (int i = 0; i < count; i++)
                {
                    swings.Add(new Swing { Type = type, Direction = h2h && swings.Count == 1 ? Direction.Right : direction, Round = round });
                    if (swings.Count == MaxAttacks) return;
                }
            }

            void CreateAttacks(int maxHit, int reqLvl, Direction direction, ushort weaponId)
            {
                int num = 1;
                if (reqLvl <= attacker.MLvl) num = HitCount(maxHit);
                if (attacker is CharData && attacker.GetMod(Mod.MAX_SWINGS) != 0)
                    num = HitCount(Math.Min((byte)attacker.GetMod(Mod.MAX_SWINGS), (byte)8));
                if (attacker is MobEntity mob && mob.GetMobMod(MobMod.MultiHit) > 0)
                    num = 1 + HitCount((byte)mob.GetMobMod(MobMod.MultiHit));

                int triple = attacker.GetMod(Mod.TRIPLE_ATTACK);
                int dbl = attacker.GetMod(Mod.DOUBLE_ATTACK);
                int quad = attacker.GetMod(Mod.QUAD_ATTACK);
                bool multiHit = false;
                bool mainHand = (h2h && swings.Count > 0) || direction == Direction.Right;
                int thrice = Math.Clamp((int)attacker.GetMod(Mod.MYTHIC_OCC_ATT_THRICE), 0, 100);
                int twice = Math.Clamp((int)attacker.GetMod(Mod.MYTHIC_OCC_ATT_TWICE), 0, 100);
                if (attacker is CharData mc)
                {
                    // the merits only with the job trait
                    if (CharStats.HasTrait(mc, TraitTripleAttack)) triple += mc.Merits?.Value(mc, MeritTripleAttackRate) ?? 0;
                    // Ambush's augment: +1% a merit from behind the target
                    if (CharStats.HasTrait(mc, TraitAmbush) && mc.GetMod(Mod.AUGMENTS_AMBUSH) > 0 && battleTarget != null &&
                        Math.Abs(battleTarget.Rotation - mc.Rotation) < 23)
                        triple += mc.Merits?.Count(MeritAmbush) ?? 0;
                    if (CharStats.HasTrait(mc, TraitDoubleAttack)) dbl += mc.Merits?.Value(mc, MeritDoubleAttackRate) ?? 0;
                }
                if (attacker is CharData && !mainHand) dbl += attacker.GetMod(Mod.EXTRA_DUAL_WIELD_ATTACK);
                quad = Math.Clamp(quad, 0, 100);
                dbl = Math.Clamp(dbl, 0, 100);
                triple = Math.Clamp(triple, 0, 100);

                // Mikage: a swing for each Utsusemi shadow, with the main weapon
                if (attacker.StatusEffects.HasStatusEffect(EffectMikage) && weaponId == MainWeaponId(attacker))
                    Add(AttackType.Normal, direction, (byte)attacker.GetMod(Mod.UTSUSEMI));
                else if (Roll(100) < quad) { Add(AttackType.Quad, direction, 4); multiHit = true; }
                else if (Roll(100) < triple) { Add(AttackType.Triple, direction, 3); multiHit = true; }
                else if (Roll(100) < dbl) { Add(AttackType.Double, direction, 2); multiHit = true; }
                else if (mainHand && Roll(100) < thrice) Add(AttackType.Normal, direction, 2);
                else if (mainHand && Roll(100) < twice) Add(AttackType.Normal, direction, 1);
                else if (num > 1) Add(AttackType.Normal, direction, num - 1);

                if (attacker is CharData && Roll(100) < attacker.GetMod(Mod.ADDITIONAL_SWING_CHANCE)) Add(AttackType.Normal, direction, 1);
                if (!multiHit) Add(AttackType.Normal, direction, 1);
            }

            // the main weapon: a mob's always hits once at most (setMaxHit(1)), a char's by its item_weapon hit
            int mainHit = 1, mainReq = 0;
            if (attacker is CharData c)
            {
                mainHit = c.MainWeapon?.Hit ?? 0;
                mainReq = c.MainWeapon?.Equip?.ReqLvl ?? 0;
            }
            if (attacker is MobEntity || ((CharData)attacker).MainWeapon != null)
            {
                if (h2h)
                {
                    bool singleSwing = attacker is MobEntity m && m.GetMobMod(MobMod.H2hSingleSwing) > 0;
                    CreateAttacks(mainHit, mainReq, Direction.Left, MainWeaponId(attacker));
                    if (!singleSwing) CreateAttacks(mainHit, mainReq, Direction.Left, MainWeaponId(attacker));
                }
                else CreateAttacks(mainHit, mainReq, Direction.Right, MainWeaponId(attacker));
            }

            if (IsDualWielding(attacker))
            {
                if (attacker is CharData pc && pc.SubWeapon is WeaponInfo sub)
                    CreateAttacks(sub.Hit, sub.Equip?.ReqLvl ?? 0, Direction.Left, sub.Equip?.ItemId ?? 0);
                else if (attacker is MobEntity)
                    CreateAttacks(0, 0, Direction.Left, 0);
            }

            if (h2h)
            {
                int kick = attacker.GetMod(Mod.KICK_ATTACK_RATE);
                if (attacker is CharData mnk && mnk.MJob == MobStats.JobMnk) kick += mnk.Merits?.Value(mnk, MeritKickAttackRate) ?? 0;
                kick = Math.Clamp(kick, 0, 100);
                bool kicked = false;
                if (Roll(100) < kick)
                {
                    Add(AttackType.Kick, Direction.Right, 1);
                    kicked = true;
                }
                if (kicked && Roll(100) < attacker.GetMod(Mod.EXTRA_KICK_ATTACK)) Add(AttackType.Kick, Direction.Left, 1);
            }

            // CreateDakenAttack: a char's shuriken thrown at DAKEN %
            if (attacker is CharData dk && dk.AmmoWeapon is { Skill: Skill.Throwing, SubSkill: SubSkillShuriken } &&
                Roll(100) < dk.GetMod(Mod.DAKEN))
                Add(AttackType.Daken, Direction.Right, 1);

            // ProcFollowUpAttacks: AMMO_SWING (a Jailer weapon's) rolled for each normal, double, triple or quad swing by
            // the weapon of its hand; a proc with a virtue stone in the ammo slot stores a follow-up (at most two, not
            // the same hand twice in a row) and spends the stone. The follow-ups come last.
            if (attacker is CharData fc && fc.GetMod(Mod.AMMO_SWING) != 0)
            {
                var followUps = new List<Direction>();
                foreach (var sw in swings.ToArray())
                {
                    if (sw.Type is not (AttackType.Normal or AttackType.Double or AttackType.Triple or AttackType.Samba or AttackType.Quad))
                        continue;
                    var weapon = h2h || sw.Direction == Direction.Right ? fc.Equipped[SlotMain] : fc.Equipped[SlotSub];
                    if (weapon == null || Roll(100) >= CharStats.ScaledItemModifier(fc, weapon, Mod.AMMO_SWING)) continue;
                    if (fc.Equipped[CharStats.SlotAmmo]?.ItemId != VirtueStone || !(CharStats.EquippedItem(fc, CharStats.SlotAmmo)?.Quantity > 0))
                        continue;
                    if (followUps.Count < 2 && (followUps.Count == 0 || followUps[^1] != sw.Direction))
                    {
                        followUps.Add(sw.Direction);
                        RemoveAmmoOf(fc, 1);
                    }
                }
                foreach (var direction in followUps) Add(AttackType.FollowUp, direction, 1);
            }

            if (swings.Count > 0) swings[0].First = true;
            // Haste Daze's haste lasts the one round
            attacker.StatusEffects.DelStatusEffect(EffectHasteSambaHaste);
            return swings;
        }

        // the id of the weapon in m_Weapons[SLOT_MAIN] (a mob's, or no weapon, is 0)
        private static ushort MainWeaponId(BattleEntity attacker) => attacker is CharData c ? c.MainWeapon?.Equip?.ItemId ?? 0 : (ushort)0;

        // CAttack::GetWeaponSlot: a Daken throw's is the ammo slot (unless hand-to-hand)
        public static int WeaponSlot(BattleEntity attacker, Swing swing) =>
            IsH2H(attacker) ? SlotMain : swing.Type == AttackType.Daken ? CharStats.SlotAmmo : swing.Direction == Direction.Right ? SlotMain : SlotSub;

        // CAttack::GetAnimationID: the kicks, the Daken throw (4), the hands
        public static ushort Animation(Swing swing) => swing.Type == AttackType.Kick
            ? (ushort)(swing.Direction == Direction.Right ? 2 : 3)
            : swing.Type == AttackType.Daken ? (ushort)4
            : (ushort)(swing.Direction == Direction.Right ? 0 : 1);

        // Sange's merit count for a Daken throw (0 without Sange)
        private static int SangeMerits(BattleEntity attacker) =>
            attacker.StatusEffects.HasStatusEffect(EffectSange) && attacker is CharData c && c.Merits != null ? c.Merits.Value(c, MeritSange) : -1;

        // CAttack::GetHitRate; a sure right-hand hit marks the round SATA (SA / TA wear on the attack, before the 2nd+ hits)
        public byte SwingHitRate(BattleEntity attacker, BattleEntity defender, Swing swing)
        {
            // a Daken throw: the ranged hit rate, +100 accuracy and 25 a Sange merit past the first
            if (swing.Type == AttackType.Daken)
            {
                int merits = SangeMerits(attacker);
                int accBonus = 100 + (merits >= 0 ? (merits - 1) * 25 : 0);
                return (byte)RangedHitRateOf(attacker, defender, (short)accBonus);
            }
            // a Zanshin swing with +35 accuracy
            byte rate = HitRate(attacker, defender, swing.Type == AttackType.Kick ? 2 : swing.Direction == Direction.Right ? 0 : 1,
                                swing.Type == AttackType.Zanshin ? 35 : 0);
            if (swing.Type != AttackType.Kick && swing.Direction == Direction.Right && rate == 100 && swing.Round != null)
                swing.Round.Sata = true;
            return rate;
        }

        // CAttack::SetCritical: the damage ratio, by the skill of the weapon in the swing's slot
        public float SwingDamageRatio(BattleEntity attacker, BattleEntity defender, Swing swing, bool isCritical)
        {
            // a Daken throw: the ranged damage ratio, ENHANCES_SANGE ranged attack a Sange merit
            if (swing.Type == AttackType.Daken)
            {
                int merits = SangeMerits(attacker);
                int bonusRatt = merits >= 0 ? attacker.GetMod(Mod.ENHANCES_SANGE) * merits : 0;
                return RangedDamageRatioOf(attacker, defender, isCritical, (ushort)bonusRatt);
            }
            byte skill = Skill.None;
            int slot = WeaponSlot(attacker, swing);
            if (attacker is CharData c) skill = CharStats.WeaponIn(c, slot)?.Skill ?? Skill.HandToHand;
            return DamageRatio(attacker, defender, isCritical, skill, slot);
        }

        // CAttack::ProcessDamage, the native path
        public int ProcessDamage(BattleEntity attacker, BattleEntity defender, Swing swing, float damageRatio)
        {
            int slot = WeaponSlot(attacker, swing);
            int damage = 0;
            int bonusBase = 0; // m_bonusBasePhysicalDamage

            // Sneak attack.
            if (attacker.MJob == JobThf && swing.First && attacker.StatusEffects.HasStatusEffect(EffectId.SneakAttack) &&
                (Behind(attacker, defender) || attacker.StatusEffects.HasStatusEffect(EffectHide) ||
                 defender.StatusEffects.HasStatusEffect(EffectDoubt)))
            {
                bonusBase += (int)(Dex(attacker) * (1.0f + Math.Max(attacker.GetMod(Mod.SNEAK_ATK_DEX) / 100.0f, 0f)));
                swing.SneakAttack = true;
            }

            // Trick attack.
            if (attacker.MJob == JobThf && swing.First && swing.Round?.TaEntity != null)
            {
                bonusBase += (int)(Agi(attacker) * (1.0f + Math.Max(attacker.GetMod(Mod.TRICK_ATK_AGI) / 100.0f, 0f)));
                swing.TrickAttack = true;
            }

            // Consume mana
            if (attacker is CharData cm) bonusBase += ConsumeMana(cm);

            if (IsH2H(attacker))
            {
                // a Monipulator's fists are all in its base damage
                int naturalH2h = attacker is CharData { Monstrosity: not null } ? 0 : (int)MathF.Floor(GetSkill(attacker, Skill.HandToHand) * 0.11f) + 3;
                int baseDamage = MainWeaponDmg(attacker);
                if (attacker is MobEntity mob)
                {
                    float penalty = 1.0f;
                    int fStr = FStr(attacker, defender);
                    if (mob.GetMobMod(MobMod.NoH2hPenalty) == 0)
                        penalty = ZoneRegions.Of(mob.Zone) <= 27 // REGION_TYPE::LIMBUS
                        ? 0.425f : 0.650f;
                    damage = baseDamage + bonusBase;
                    if (swing.Type == AttackType.Kick)
                    {
                        const float kickPenalty = 2.0f / 3.0f;
                        damage = (int)((damage + mob.GetMod(Mod.KICK_DMG)) * penalty * kickPenalty + fStr);
                    }
                    else damage = (int)((damage + fStr) * penalty);
                    damage = Math.Max(damage, 0);
                    damage = (int)(uint)MathF.Floor(damage * damageRatio);
                }
                else if (swing.Type == AttackType.Kick)
                {
                    int kickDamage = naturalH2h + attacker.GetMod(Mod.KICK_DMG);
                    damage = Math.Max(kickDamage + bonusBase + FStr(attacker, defender), 0);
                    damage = (int)(uint)MathF.Floor(damage * damageRatio);
                }
                else
                {
                    damage = Math.Max(baseDamage + naturalH2h + bonusBase + FStr(attacker, defender), 0);
                    damage = (int)(uint)MathF.Floor(damage * damageRatio);
                }
            }
            else if (slot == SlotMain)
            {
                damage = Math.Max(MainWeaponDmg(attacker) + bonusBase + FStr(attacker, defender), 0);
                damage = (int)(uint)MathF.Floor(damage * damageRatio);
            }
            else if (slot == CharStats.SlotAmmo)
            {
                // a Daken throw: the ranged weapon damage and the ranged fSTR
                damage = Math.Max(RangedBaseDamageOf(attacker, defender), 0);
                damage = (int)(uint)MathF.Floor(damage * damageRatio);
            }
            else
            {
                damage = Math.Max(SubWeaponDmg(attacker) + bonusBase + FStr(attacker, defender), 0);
                damage = (int)(uint)MathF.Floor(damage * damageRatio);
            }

            // Scarlet Delirium's bonus, once the DRK has taken damage under it
            if (attacker.StatusEffects.GetStatusEffect(EffectScarletDelirium1) is { } delirium)
                damage = (int)(uint)MathF.Floor(damage * Math.Max(1.0f + delirium.Power / 1000.0f, 0f));

            if (attacker is CharData c)
            {
                // the C++ adds the bonus fraction rather than scaling by it
                if (swing.Type == AttackType.Double)
                    damage = (int)(uint)MathF.Floor(damage * 1.0f + Math.Max(c.GetMod(Mod.DOUBLE_ATTACK_DMG) / 100.0f, 0f));
                else if (swing.Type == AttackType.Triple)
                    damage = (int)(uint)MathF.Floor(damage * 1.0f + Math.Max(c.GetMod(Mod.TRIPLE_ATTACK_DMG) / 100.0f, 0f));
                damage = SoulEater(c, damage);
            }

            // a normal swing under Drain, Aspir or Haste Samba counts as a samba one (SAMBA_DOUBLE_DAMAGE)
            var type = swing.Type;
            if (type == AttackType.Normal && (attacker.StatusEffects.HasStatusEffect(EffectDrainSamba) ||
                                              attacker.StatusEffects.HasStatusEffect(EffectAspirSamba) ||
                                              attacker.StatusEffects.HasStatusEffect(EffectHasteSamba)))
                type = AttackType.Samba;

            if (attacker is CharData pc)
                damage = (int)DamageMultiplier(pc, CharStats.WeaponIn(pc, slot), (uint)damage, type, slot, swing.First);

            // Apply Sneak Attack / Trick Attack Augment Mod
            if (attacker.GetMod(Mod.AUGMENTS_SA) > 0 && swing.SneakAttack && attacker.StatusEffects.HasStatusEffect(EffectId.SneakAttack))
                damage = (int)(uint)MathF.Floor(damage * (1.0f + Math.Max(attacker.GetMod(Mod.AUGMENTS_SA) / 100.0f, 0f)));
            if (attacker.GetMod(Mod.AUGMENTS_TA) > 0 && swing.TrickAttack && attacker.StatusEffects.HasStatusEffect(EffectId.TrickAttack))
                damage = (int)(uint)MathF.Floor(damage * (1.0f + Math.Max(attacker.GetMod(Mod.AUGMENTS_TA) / 100.0f, 0f)));
            damage = Math.Max(damage, 0);

            // Restraint: the first swing adds to the weapon skill damage bonus (the effect's power, to +30 %), the
            // floored remainder kept in its sub power (x 100) for the next round
            if (swing.First && attacker.StatusEffects.GetStatusEffect(EffectRestraint) is { } restraint && restraint.Power < 30)
            {
                int jpBonus = attacker is CharData rc ? (byte)(JobPointUtil.Value(rc, JpRestraintEffect) * 2) : 0;
                float boostPerRound = WeaponDelay(attacker, false) / 1000.0f * 60.0f / 385.0f;
                float remainder = restraint.SubPower / 100.0f;
                boostPerRound = boostPerRound * (1 + attacker.GetMod(Mod.ENHANCES_RESTRAINT) / 100.0f) * (1 + jpBonus / 100.0f) + remainder;
                remainder = (1 - (MathF.Ceiling(boostPerRound) - boostPerRound)) * 100;
                boostPerRound = MathF.Floor(boostPerRound);
                if (restraint.Power + boostPerRound > 30) boostPerRound = 30 - restraint.Power;
                restraint.Power = (ushort)(restraint.Power + boostPerRound);
                restraint.SubPower = (ushort)remainder;
                attacker.AddModifier(Mod.ALL_WSDMG_FIRST_HIT, (short)boostPerRound);
            }
            return damage;
        }

        // attackutils::CheckForDamageMultiplier for a melee swing's attack type
        private uint DamageMultiplier(CharData c, WeaponInfo weapon, uint damage, AttackType type, int slot, bool allowProc) =>
            ZoneHost.PhysicalDamageMultiplier(c, weapon, damage, type switch
            {
                AttackType.Double => ZoneHost.PhysicalAttackType.Double,
                AttackType.Triple => ZoneHost.PhysicalAttackType.Triple,
                AttackType.Quad => ZoneHost.PhysicalAttackType.Quad,
                AttackType.Kick => ZoneHost.PhysicalAttackType.Kick,
                AttackType.Zanshin => ZoneHost.PhysicalAttackType.Zanshin,
                AttackType.Samba => ZoneHost.PhysicalAttackType.Samba,
                AttackType.Daken => ZoneHost.PhysicalAttackType.Daken,
                AttackType.FollowUp => ZoneHost.PhysicalAttackType.Followup,
                _ => ZoneHost.PhysicalAttackType.Normal,
            }, slot, allowProc, Rng);

        // battleutils::doConsumeManaEffect: under Consume Mana a tenth of the char's MP adds to the base damage; the MP
        // goes to 0 (no update flag, as C++) and the effect with it
        private static int ConsumeMana(CharData c)
        {
            if (!c.StatusEffects.HasStatusEffect(EffectConsumeMana)) return 0;
            int bonus = c.Mp / 10;
            c.Mp = 0;
            c.StatusEffects.DelStatusEffect(EffectConsumeMana);
            return bonus;
        }

        // battleutils::doSoulEaterEffect: under Souleater a tenth of the char's HP (plus the best gear's SOULEATER_EFFECT
        // and SOULEATER_EFFECT_II) adds to the swing, all of it for a DRK, half otherwise; the char pays it, less
        // STALWART_SOUL, through stoneskin. The uint16 return wraps as C++'s does.
        private int SoulEater(CharData c, int damage)
        {
            if (c.StatusEffects.HasStatusEffect(EffectSouleater))
            {
                float souleaterBonus = (float)(ZoneHost.MaxGearMod(c, Mod.SOULEATER_EFFECT) * 0.01);
                float souleaterBonusII = (float)(c.GetMod(Mod.SOULEATER_EFFECT_II) * 0.01);
                float stalwartSoulBonus = 1.0f - Math.Max(c.GetMod(Mod.STALWART_SOUL) / 100.0f, 0f);
                float bonusDamage = c.Hp * (0.1f + Math.Max(souleaterBonus + souleaterBonusII, 0f));
                if (bonusDamage >= 1)
                {
                    AddCharHp(c, -c.StatusEffects.HandleStoneskin((int)(bonusDamage * stalwartSoulBonus)));
                    damage = (int)(uint)(c.MJob == JobDrk ? (uint)damage + bonusDamage : (uint)damage + bonusDamage / 2);
                }
            }
            return (ushort)damage;
        }

        // battleutils::HandleFanDance: Fan Dance's power in 1/100 % off, 10 % less each hit down to 20 %
        public static int HandleFanDance(BattleEntity defender, int damage)
        {
            if (defender.StatusEffects.GetStatusEffect(EffectFanDance) is { } fanDance)
            {
                int power = fanDance.Power;
                damage = (int)(damage * (1.0f - power / 10000.0f));
                if (power > 2000) fanDance.Power = (ushort)(power - 1000);
            }
            return damage;
        }

        // ---- damage taken (battleutils::TakePhysicalDamage's damage side) --------------------------------------------

        // battleutils::CheckAndApplyDamageCap
        public int DamageCap(BattleEntity defender, int damage)
        {
            int cap = defender.GetMod(Mod.RECEIVED_DAMAGE_CAP);
            if (cap == 0 || damage < cap) return damage;
            int variant = defender.GetMod(Mod.RECEIVED_DAMAGE_VARIANT);
            if (variant > cap) variant = 0;
            damage = Math.Clamp(damage, 0, cap);
            damage -= variant + 1 <= 0 ? 0 : Rng.Next(variant + 1);
            return Math.Clamp(damage, cap - variant, cap);
        }

        // battleutils::PhysicalDmgTaken: the resists, the auto equalizer, an absorb or a null, else HandleSevereDamage,
        // ConvertDmgToMP (ABSORB_PHYSDMG_TO_MP percent of what lands, and COVER_TO_MP more of a covered blow, into MP
        // through addMp) and Fan Dance
        private int PhysicalDmgTaken(BattleEntity defender, int damage, bool isCovered, Action<int> addMp, bool ranged = false)
        {
            // battleutils::RangedDmgTaken for a ranged blow: its own resists (no Burtgang past the cap) and null
            float resist = Math.Max(1.0f + defender.GetMod(ranged ? Mod.UDMGRANGE : Mod.UDMGPHYS) / 10000.0f, 0f);
            damage = (int)(damage * resist);
            resist = Math.Max(1.0f + defender.GetMod(ranged ? Mod.DMGRANGE : Mod.DMGPHYS) / 10000.0f + defender.GetMod(Mod.DMG) / 10000.0f, 0.5f);
            if (!ranged) resist += defender.GetMod(Mod.DMGPHYS_II) / 10000.0f;
            damage = (int)(damage * resist);
            if (damage > 0 && defender.GetMod(Mod.AUTO_EQUALIZER) > 0)
            {
                float reductionRate = MathF.Floor(damage / (float)defender.MaxHp * defender.GetMod(Mod.AUTO_EQUALIZER)) / 100.0f;
                damage = (int)MathF.Floor(damage * (1.0f - Math.Min(reductionRate, 0.90f)));
            }
            if (Roll(100) < defender.GetMod(Mod.ABSORB_DMG_CHANCE) || Roll(100) < defender.GetMod(Mod.PHYS_ABSORB))
                damage = -damage;
            else if (Roll(100) < defender.GetMod(Mod.NULL_DAMAGE) || Roll(100) < defender.GetMod(ranged ? Mod.NULL_RANGED_DAMAGE : Mod.NULL_PHYSICAL_DAMAGE))
                damage = 0;
            else
            {
                damage = SevereDamage(defender, damage, true);
                double dmgToMpMods = (isCovered ? defender.GetMod(Mod.COVER_TO_MP) : 0) + defender.GetMod(Mod.ABSORB_PHYSDMG_TO_MP);
                short absorbedMp = (short)(damage * (dmgToMpMods / 100.0));
                if (absorbedMp > 0) addMp?.Invoke(absorbedMp);
                damage = HandleFanDance(defender, damage);
            }
            return DamageCap(defender, damage);
        }

        // the damage a melee swing lands with, before takeDamage: under Formless Strikes (not a counter) 55% (+ the
        // merits) through the breath resists, an absorb or a null, else the physical resists, the damage-type SDT (a
        // counter's hand-to-hand, unless it gives the counterer TP: Retaliation's, and never an absorb) and a shield block
        // (Reprisal's power in % of the blocked share as the defender's SPIKES_DMG, from the damage before the resists under
        // Invincible or Sentinel); then Phalanx, stoneskin (a physical-only one stops no Formless Strikes, a magical-only one
        // no physical blow), Afflatus Misery's pool, the damage cap and Scarlet Delirium's switch. Negative is an absorb.
        // shieldMasteryTp gets SHIELD_MASTERY_TP when a block leaves damage past stoneskin.
        public int MeleeDamageTaken(BattleEntity attacker, BattleEntity defender, int damage, int slot, bool isBlocked = false,
            bool isCounter = false, Action<int> shieldMasteryTp = null, bool isCovered = false, Action<int> addMp = null,
            bool giveTpToAttacker = false)
        {
            if (IsFormless(attacker, isCounter)) return FinishDamageTaken(defender, FormlessDmgTaken(attacker, defender, damage), true);
            uint damageType = attacker is CharData c ? CharStats.WeaponIn(c, slot)?.DmgType ?? DamageType.None : DamageType.None;
            int baseDamage = damage;
            // an ammo-slot swing (a Daken throw) takes the ranged path
            damage = PhysicalDmgTaken(defender, damage, isCovered, addMp, slot == CharStats.SlotAmmo || slot == CharStats.SlotRanged);
            if (damage < 0 && isCounter) damage = -damage;
            Mod? sdt = isCounter && !giveTpToAttacker ? Mod.HTH_SDT : damageType switch
            {
                DamageType.Piercing => Mod.PIERCE_SDT,
                DamageType.Slashing => Mod.SLASH_SDT,
                DamageType.Blunt => Mod.IMPACT_SDT,
                DamageType.HandToHand => Mod.HTH_SDT,
                _ => null,
            };
            if (sdt is Mod m) damage = (int)(damage * (1.0f + defender.GetMod(m) / 10000.0f));
            if (isBlocked)
            {
                int absorb = 50;
                if (damage > 0) damage = Math.Max(0, damage - defender.GetMod(Mod.SHIELD_DEF_BONUS));
                if (defender.GetMod(Mod.SHIELD_MASTERY_TP) != 0)
                {
                    int stoneskinPower = defender.StatusEffects.GetStatusEffect(EffectId.Stoneskin)?.Power ?? 0;
                    if (damage - stoneskinPower > 0) shieldMasteryTp?.Invoke(defender.GetMod(Mod.SHIELD_MASTERY_TP));
                }
                if (defender is CharData pc && pc.Equipped[SlotSub] is { IsShield: true } shield)
                    absorb = Math.Clamp(100 - shield.ShieldAbsorption, 0, 100);
                // Reprisal: a share of the blocked damage back, before stoneskin, Phalanx, Sentinel or Invincible
                if (damage > 0 && defender.StatusEffects.GetStatusEffect(EffectReprisal) is { } reprisal)
                {
                    float spikesBonus = 1.0f + defender.GetMod(Mod.REPRISAL_SPIKES_BONUS) / 100.0f;
                    short effectPower = (short)(reprisal.Power * spikesBonus);
                    int blockedDamage = damage * (100 - absorb) / 100;
                    if (defender.StatusEffects.HasStatusEffect(EffectInvincible, EffectSentinel))
                        blockedDamage = (int)(baseDamage * (100.0f - absorb) / 100.0f);
                    defender.SetModifier(Mod.SPIKES_DMG, (short)(int)(blockedDamage * (effectPower / 100.0f)));
                }
                damage = damage * absorb / 100;
            }
            return FinishDamageTaken(defender, damage, false);
        }

        // TakePhysicalDamage's Formless Strikes test: the attacker under it, not countering
        public static bool IsFormless(BattleEntity attacker, bool isCounter) =>
            attacker.StatusEffects.HasStatusEffect(EffectFormlessStrikes) && !isCounter;

        // TakePhysicalDamage's Formless Strikes branch (bg-wiki Formless_Strikes): 55% of the damage, +5% a merit, as
        // breath damage taken (the resist held to 0.5 - 1.5), an absorb or a null by chance, and the damage cap
        internal int FormlessDmgTaken(BattleEntity attacker, BattleEntity defender, int damage)
        {
            int formlessMod = 55;
            if (attacker is CharData fc) formlessMod += fc.Merits?.Value(fc, MeritFormlessStrikes) ?? 0;
            damage = damage * formlessMod / 100;
            float resist = Math.Max(1.0f + defender.GetMod(Mod.UDMGBREATH) / 10000.0f, 0.0f);
            damage = (int)(damage * resist);
            resist = Math.Clamp(1.0f + defender.GetMod(Mod.DMGBREATH) / 10000.0f + defender.GetMod(Mod.DMG) / 10000.0f, 0.5f, 1.5f);
            damage = (int)(damage * resist);
            if (Roll(100) < defender.GetMod(Mod.ABSORB_DMG_CHANCE)) damage = -damage;
            else if (Roll(100) < defender.GetMod(Mod.NULL_DAMAGE) || Roll(100) < defender.GetMod(Mod.NULL_BREATH_DAMAGE)) damage = 0;
            else damage = SevereDamage(defender, damage, false);
            return DamageCap(defender, damage);
        }

        // TakePhysicalDamage after the damage taken: Phalanx, stoneskin by the attack type, Afflatus Misery, the clamp,
        // the damage cap and Scarlet Delirium
        private int FinishDamageTaken(BattleEntity defender, int damage, bool special)
        {
            if (damage > 0)
            {
                damage = Math.Max(damage - defender.GetMod(Mod.PHALANX), 0);
                // HandleStoneskin(PDefender, damage, attackType): subtype 2 (magical only) lets a physical blow through,
                // subtype 1 (physical only) a special one
                if (damage > 0 && defender.StatusEffects.GetStatusEffect(EffectId.Stoneskin) is { } stoneskin &&
                    !(stoneskin.SubId == 2 && !special) && !(stoneskin.SubId == 1 && special))
                    damage = defender.StatusEffects.HandleStoneskin(damage);
                // HandleAfflatusMiseryDamage: the pool Afflatus Misery's next Banish / Auspice reads
                if (damage > 0 && defender.StatusEffects.HasStatusEffect(EffectAfflatusMisery))
                    defender.SetModifier(Mod.AFFLATUS_MISERY, (short)damage);
            }
            damage = Math.Clamp(damage, -99999, 99999);
            damage = DamageCap(defender, damage);
            HandleScarletDelirium(defender, damage);
            return damage;
        }

        // battleutils::HandleScarletDelirium: the first damage taken turns the absorbing effect into the damage bonus one,
        // its power from the damage's share of max HP (to 50%), for 90 s plus the job points' seconds
        internal static void HandleScarletDelirium(BattleEntity defender, int damage)
        {
            if (defender.StatusEffects.GetStatusEffect(EffectScarletDelirium) is not { Power: 0 } effect) return;
            float hppRatio = Math.Clamp((float)damage / defender.MaxHp / 2.0f, 0.0f, 0.5f);
            ushort power = (ushort)MathF.Floor(hppRatio * 1000);
            long durationMs = (90 + effect.SubPower) * 1000L;
            defender.StatusEffects.DelStatusEffectSilent(EffectScarletDelirium);
            defender.StatusEffects.AddStatusEffect(new StatusEffect(EffectScarletDelirium1, EffectScarletDelirium1, power, 0, durationMs),
                                                   EffectNotice.Silent);
        }

        // ---- TP (scripts/combat/basic/tp.lua) ------------------------------------------------------------------------

        // xi.combat.tp.calculateTPReturn
        public static int TpReturn(BattleEntity gainee, double delay)
        {
            double tp;
            // a mob's formula, but a PC's for a mob a PC has charmed (pets, trusts and PCs use it too)
            bool charmedPcPet = IsMobObject(gainee) && gainee.PMaster is CharData && gainee.IsCharmed;
            if (IsMobObject(gainee) && !charmedPcPet)
            {
                tp = delay > 530 ? 145 + (delay - 530) * 35 / 470
                   : delay > 480 ? 130 + (delay - 480) * 15 / 50
                   : delay > 450 ? 115 + (delay - 450) * 15 / 30
                   : delay > 180 ? 50 + (delay - 180) * 65 / 270
                   : 50 + (delay - 180) * 15 / 180;
            }
            else
            {
                tp = delay > 900 ? 173 + (delay - 900) * 28 / 360
                   : delay > 720 ? 161 + (delay - 720) * 24 / 360
                   : delay > 630 ? 154 + (delay - 630) * 28 / 360
                   : delay > 540 ? 149 + (delay - 540) * 20 / 360
                   : delay > 180 ? 61 + (delay - 180) * 88 / 360
                   : 61 + (delay - 180) * 63 / 360;
            }
            return (int)Math.Floor(tp);
        }

        // xi.combat.tp.getModifiedDelayAndCanZanshin's canZanshin: a single swing (one-handed or two-handed), a
        // monipulator's, or one fist (a shield on, or no hand-to-hand rank)
        public static bool CanZanshin(BattleEntity actor)
        {
            if (actor is CharData mon && mon.Monstrosity != null) return true;
            if (IsDualWielding(actor)) return false;
            if (actor is CharData c && UsingH2H(c)) return c.Equipped[SlotSub] != null || c.RealSkillRank[Skill.HandToHand] == 0;
            if (actor is MobEntity && IsH2H(actor)) return false;
            return true;
        }

        // xi.combat.tp.getModifiedDelay: dual wield halves the delay, H2H takes Martial Arts off and halves it (a mob's
        // and a jug pet's without Martial Arts)
        public static int ModifiedDelay(BattleEntity actor, double delay)
        {
            // a Monipulator's base delay is already per hit
            if (actor is CharData { Monstrosity: not null }) { }
            else if (IsDualWielding(actor)) delay = delay * (100 - actor.GetMod(Mod.DUAL_WIELD)) / 100 / 2;
            else if (actor is CharData c && UsingH2H(c))
            {
                if (c.Equipped[SlotSub] != null || c.RealSkillRank[Skill.HandToHand] == 0)
                    delay = Math.Max(delay - c.GetMod(Mod.MARTIAL_ARTS), 96);
                else delay = Math.Max((delay - c.GetMod(Mod.MARTIAL_ARTS)) / 2, 48);
            }
            else if (actor is MobEntity && IsH2H(actor))
            {
                if (IsMobObject(actor) || actor is PetEntity jug && PetId.IsJugPet(jug.PetId)) delay = Math.Max(delay / 2, 48);
                else delay = Math.Max((delay - actor.GetMod(Mod.MARTIAL_ARTS)) / 2, 48);
            }
            delay *= Math.Max((100 + actor.GetMod(Mod.DELAYP)) / 100.0, 0.85);
            return (int)Math.Floor(delay);
        }

        // battleutils::GetBaseDelay / CBattleEntity's weapon base delays
        public static int BaseDelay(BattleEntity e)
        {
            if (e is MobEntity m) return m.BaseDelay;
            var c = (CharData)e;
            if (c.Monstrosity != null) return Monstrosity.BaseDelay(c.Monstrosity);
            if (c.Equipped[SlotMain] == null) return 480;
            var main = c.MainWeapon;
            if (main == null) return 480;
            if (main.HandToHand) return (int)main.Delay;
            int delay = (int)main.Delay;
            if (c.SubWeapon is WeaponInfo sub && sub.Skill != Skill.None) delay += (int)sub.Delay;
            return delay;
        }

        // xi.combat.tp.getSingleMeleeHitTPReturn, with the Store TP merit (a SAM's); a Zanshin swing's adds the Ikishoten
        // merits when the round could take one
        public static int SingleMeleeHitTpReturn(BattleEntity actor, bool isZanshin = false)
        {
            if (actor.StatusEffects.HasStatusEffect(EffectMeikyoShisui)) return 0;
            int tp = TpReturn(actor, ModifiedDelay(actor, BaseDelay(actor)));
            if (isZanshin && CanZanshin(actor) && actor is CharData ik) tp += ik.Merits?.Value(ik, MeritIkishoten) ?? 0;
            int merit = actor is CharData c ? c.Merits?.Value(c, MeritStoreTpEffect) ?? 0 : 0;
            return (int)Math.Floor(tp * (1 + (actor.GetMod(Mod.STORETP) + merit) / 100.0));
        }

        // xi.combat.tp.getTandemBlowBonus: Tandem Blow's subtle blow II (the master's power for a PC's pet)
        public static int TandemBlowBonus(BattleEntity actor)
        {
            if (!IsTandemActive(actor)) return 0;
            return actor.PMaster is CharData master ? master.GetMod(Mod.TANDEM_BLOW_POWER) : actor.GetMod(Mod.TANDEM_BLOW_POWER);
        }

        // xi.combat.tp.calculateTPGainOnPhysicalDamage: the TP the defender gains from a hit, a mob's (base + 30 with
        // the dAGI penalty) when a non-mob hits it, else a third
        public static int TpGainOnPhysicalDamage(BattleEntity actor, BattleEntity target, int damage, int delay)
        {
            if (damage <= 0) return 0;
            if (actor.StatusEffects.HasStatusEffect(EffectMeikyoShisui)) return 0;
            double baseTp = TpReturn(actor, ModifiedDelay(actor, delay));
            int dAgi = Agi(actor) - Agi(target);
            double inhibit = (100 - target.GetMod(Mod.INHIBIT_TP)) / 100.0;
            double dAgiMod = Math.Clamp((200 - (dAgi + 30)) / 200.0, 0.5, 1);
            int sbMerits = actor is CharData mc ? mc.Merits?.Value(mc, MeritSubtleBlowEffect) ?? 0 : 0;
            int sb1 = Math.Min(actor.GetMod(Mod.SUBTLE_BLOW) + sbMerits, 50);
            int sb2 = actor.GetMod(Mod.SUBTLE_BLOW_II) + TandemBlowBonus(actor);
            double sbMod = Math.Max((100 - sb1 + sb2) / 100.0, 0.25);
            double storeTp = 1 + target.GetMod(Mod.STORETP) / 100.0;
            if (IsMobObject(target) && !IsMobObject(actor))
                return (int)Math.Floor((baseTp + 30) * inhibit * dAgiMod * sbMod * storeTp);
            return (int)Math.Floor(baseTp * inhibit * sbMod * storeTp / 3);
        }

        // ---- swing delay (CBattleEntity::GetWeaponDelay) ------------------------------------------------------------

        public int WeaponDelay(BattleEntity e, bool tp)
        {
            int mainDelayMs = e switch
            {
                CharData c => c.Monstrosity != null ? Monstrosity.WeaponDelay(c.Monstrosity) : c.MainWeapon?.DelayMs ?? 8000,
                MobEntity m => (ushort)Math.Min(m.MainDelay * 1000.0f / 60.0f, 65535.0f),
                _ => 8000,
            };
            uint weaponDelay = (ushort)(mainDelayMs + e.GetMod(Mod.DELAY));
            uint martialArts = 0;
            float dualWield = 1.0f;
            // mobs and Monipulators do not benefit from Martial Arts
            if (IsH2H(e) && e is CharData { Monstrosity: null })
                martialArts = (uint)(e.GetMod(Mod.MARTIAL_ARTS) * 1000 / 60);
            else if (e is CharData pc && pc.SubWeapon is WeaponInfo sub && sub.DmgType > DamageType.None && sub.DmgType < DamageType.HandToHand)
            {
                weaponDelay += (uint)sub.DelayMs;
                dualWield = 1.0f - e.GetMod(Mod.DUAL_WIELD) / 100.0f;
            }

            float haste = 1.0f;
            if (!tp && !(e is MobEntity mob && mob.GetMobMod(MobMod.AttackSkillList) != 0))
            {
                float hasteMagic = Math.Clamp(e.GetMod(Mod.HASTE_MAGIC) / 10000.0f, -1.0f, 0.4375f);
                int abilityRaw = e.GetMod(Mod.HASTE_ABILITY) + (MainTwoHanded(e) ? e.GetMod(Mod.TWOHAND_HASTE_ABILITY) : 0);
                float hasteAbility = Math.Clamp(abilityRaw / 10000.0f, -0.25f, 0.25f);
                float hasteGear = Math.Min(e.GetMod(Mod.HASTE_GEAR) / 10000.0f, 0.25f);
                haste = Math.Clamp(1.0f - hasteMagic - hasteAbility - hasteGear, 1.0f - cfg_.DelayReductionCap, 2.0f);
            }
            float delayMod = 1.0f + e.GetMod(Mod.DELAYP) / 100.0f;
            uint minDelay = (ushort)(weaponDelay * 0.2f);
            uint maxDelay = weaponDelay * 2;
            uint final = weaponDelay - martialArts;
            final = (uint)(final * dualWield);
            final = (uint)(final * haste);
            final = (uint)(final * delayMod);
            return (int)Math.Clamp(final, minDelay, maxDelay);
        }

        // ---- level correction zones (scripts/data/level_correction.lua) ---------------------------------------------

        private static readonly HashSet<ushort> LevelCorrectionZones = new HashSet<ushort>
        {
            1, 2, 3, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 17, 19, 21, 23, 24, 25, 26, 27, 28, 29, 30, 31,
            32, 33, 34,             35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 51, 54, 55, 56,
            57, 58, 59, 60, 61, 62, 63, 64, 65,             66, 67, 68, 69, 71, 73, 74, 75, 76, 77, 78, 79,
            80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94,             95, 96, 97, 98, 99, 100,
            101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116,             117,
            118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 132, 134, 135, 136, 137, 138,
            139,             140, 141, 142, 143, 144, 145, 146, 147, 148, 149, 150, 151, 152, 153, 154, 155,
            156, 157, 158, 159, 160,             161, 162, 163, 164, 165, 166, 167, 168, 169, 170, 172, 173,
            174, 175, 176, 177, 178, 180, 181, 182, 183,             184, 185, 186, 187, 188, 191, 192, 193,
            194, 195, 196, 197, 198, 200, 201, 202, 203, 204, 205, 206, 207,             208, 209, 211, 212,
            213, 215, 216, 217, 218, 220, 221, 222, 223, 224, 225, 226, 227, 228, 253, 254, 255,
            279, 294, 295, 296, 297, 298,
        };
    }
}
