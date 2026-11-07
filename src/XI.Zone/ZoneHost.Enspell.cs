using System;
using System.Linq;
using XI.Script;

namespace XI.Zone
{
    // battleutils::HandleEnspell: a melee swing's additional effect, run after a hit, guard or block on a defender
    // with HP left. In order: a THF's Treasure Hunter proc on a mob (the first swing; it raises the mob's TH level),
    // the attacker's samba putting its daze on the defender, then the first that applies of a priority weapon's add
    // effect (ITEM_ADDEFFECT_PRIORITY), an enspell (Blood Weapon, a rune's enhancement, Auspice, tier I / II, Enlight /
    // Endark), the weapon's add effect (xi.additionalEffect.attack, or the item script's onItemAdditionalEffect with
    // ITEM_ADDEFFECT_SCRIPTED), a grip's, a mob's or an automaton's script (onAdditionalEffect /
    // xi.pets.automaton.onAdditionalEffectAttack), and the defender's daze from the attacker's side (Drain, Aspir or
    // Haste Samba). battleutils::CalculateEnspellDamage gives an enspell's damage.
    // map.DISABLE_TREASURE_HUNTER_PROCS turns the Treasure Hunter procs off.
    public sealed partial class ZoneHost
    {
        // xi::StatusEffect
        private const ushort EffectEnlight = 274, EffectEndark = 288, EffectAspirSamba = 369, EffectDrainDaze = 378,
            EffectAspirDaze = 379, EffectHasteDaze = 380, EffectComposure = 419, EffectOneForAll = 538, EffectHasteSambaHaste = 796;

        // ActionProcAddEffect
        private const byte ProcAddNone = 0, ProcAddLightDamage = 7, ProcAddHpDrain = 21, ProcAddMpDrain = 22, ProcAddHaste = 23;

        // msg_basic.h, msg_std.h
        private const ushort MsgAddEffectHpDrained = 161, MsgAddEffectMpDrained = 162, MsgAddEffectDamage = 163,
            MsgAddEffectAdditionalDamage = 229, MsgAddEffectRecoversHp = 384, MsgStdTreasureHunterProc = 603;

        // ENSPELL (battleutils.h)
        private const int EnspellIDark = 8, EnspellILight = 7, EnspellIIDark = 16, EnspellBloodWeapon = 17, EnspellAuspice = 18;

        private const int TraitTreasureHunter = 19, MeritEnspellDamage = 2324, JpBloodWeaponEffect = 0x100;
        private const byte DamageTypeElemental = 5;
        private const byte AttackTypeMagical = 2;

        // the element's resist (MEVA), absorb and null mods, fire .. dark
        private static readonly Mod[] ElementMeva =
            { Mod.FIRE_MEVA, Mod.ICE_MEVA, Mod.WIND_MEVA, Mod.EARTH_MEVA, Mod.THUNDER_MEVA, Mod.WATER_MEVA, Mod.LIGHT_MEVA, Mod.DARK_MEVA };
        private static readonly Mod[] ElementAbsorb =
            { Mod.FIRE_ABSORB, Mod.ICE_ABSORB, Mod.WIND_ABSORB, Mod.EARTH_ABSORB, Mod.LTNG_ABSORB, Mod.WATER_ABSORB, Mod.LIGHT_ABSORB, Mod.DARK_ABSORB };
        private static readonly Mod[] ElementNull =
            { Mod.FIRE_NULL, Mod.ICE_NULL, Mod.WIND_NULL, Mod.EARTH_NULL, Mod.LTNG_NULL, Mod.WATER_NULL, Mod.LIGHT_NULL, Mod.DARK_NULL };
        // the element its day and weather are weak to (water's for fire, light's and dark's each other)
        private static readonly byte[] EnspellWeakElement = { 6, 1, 2, 3, 4, 5, 8, 7 };

        private static bool IsUndead(BattleEntity e) => e is MobEntity m && m.Ecosystem == Ecosystem.Undead;

        // the swing's weapon: a char's in the slot, else none (a mob's m_Weapons carry no item)
        private static WeaponInfo EnspellWeapon(BattleEntity attacker, int slot) => attacker is CharData c ? CharStats.WeaponIn(c, slot) : null;

        private void HandleEnspell(BattleEntity attacker, BattleEntity defender, ActionResult r, Melee.Swing swing, int slot,
            int finalDamage, long now)
        {
            var pc = attacker as CharData;
            var weapon = EnspellWeapon(attacker, slot);

            // Treasure Hunter takes priority over enspells
            if (pc != null && !ChatCfg.DisableTreasureHunterProcs && finalDamage > 0 && swing.First && defender is MobEntity thMob && pc.MJob == MobStats.JobThf &&
                CharStats.HasTrait(pc, TraitTreasureHunter) && thMob.ThLvl < 12 + pc.GetMod(Mod.TREASURE_HUNTER_CAP))
            {
                short playerTh = pc.GetMod(Mod.TREASURE_HUNTER);
                int thDiff = thMob.ThLvl - playerTh;
                // auto upgrade TH to the mod's level up to TH8
                if (thDiff < 0 && thMob.ThLvl < 8)
                {
                    thMob.ThLvl = Math.Min((short)8, playerTh);
                    thDiff = thMob.ThLvl - playerTh;
                }
                float procRate = 0.04f / MathF.Pow(2.0f, Math.Max(0, thDiff));
                float procRateBonus = 1.0f + (pc.GetMod(Mod.TREASURE_HUNTER_PROC) + thMob.GetMod(Mod.TREASURE_HUNTER_PROC)) / 100.0f;
                // Sneak Attack and Trick Attack each 10x, added together
                float sataBonus = (swing.SneakAttack ? 10.0f : 0f) + (swing.TrickAttack ? 10.0f : 0f);
                if (sataBonus > 1.0f) procRateBonus *= sataBonus;
                procRate *= procRateBonus;
                if ((float)MobRng.NextDouble() <= procRate)
                {
                    thMob.ThLvl++;
                    r.AdditionalEffect = ProcAddLightDamage; // looks like enlight, as in the capture
                    r.AddEffectMessage = MsgStdTreasureHunterProc;
                    r.AddEffectParam = thMob.ThLvl;
                    return;
                }
            }

            r.AdditionalEffect = ProcAddNone;
            r.AddEffectMessage = 0;
            r.AddEffectParam = 0;

            // the attacker's samba: its daze on the defender, the side's earlier dazes off
            ushort previousDaze = EffectId.None;
            ushort previousDazePower = 0;
            if (attacker.StatusEffects.GetStatusEffect(EffectDrainSamba) is { } drainSamba && !IsUndead(defender))
            {
                previousDaze = EffectDrainDaze;
                previousDazePower = drainSamba.Power;
            }
            else if (attacker.StatusEffects.GetStatusEffect(EffectAspirSamba) is { } aspirSamba)
            {
                previousDaze = EffectAspirDaze;
                previousDazePower = aspirSamba.Power;
            }
            else if (attacker.StatusEffects.GetStatusEffect(EffectHasteSamba) is { } hasteSamba)
            {
                previousDaze = EffectHasteDaze;
                previousDazePower = hasteSamba.Power;
            }
            if (previousDaze != EffectId.None)
            {
                void DelDazes(uint id)
                {
                    defender.StatusEffects.DelStatusEffect(EffectDrainDaze, id);
                    defender.StatusEffects.DelStatusEffect(EffectHasteDaze, id);
                    defender.StatusEffects.DelStatusEffect(EffectAspirDaze, id);
                }
                if (pc?.Party != null)
                    foreach (var member in pc.Party.Members.ToArray()) DelDazes(member.Id);
                else if (attacker is TrustEntity trust && trust.PMaster is CharData trustMaster)
                    foreach (var member in PartyWithTrusts(trustMaster)) DelDazes(member.Id);
                else DelDazes(attacker.Id);
                // (an undead mob never gets here with Drain Samba up)
                defender.StatusEffects.AddStatusEffect(new StatusEffect(previousDaze, 0, previousDazePower, 0, 10_000, attacker.Id),
                                                       EffectNotice.Silent);
            }

            // the weapon's own additional effect: the global one (additional_effects.lua) or the item script's
            bool CheckWeaponAdditionalEffect()
            {
                if (pc == null || weapon?.Equip == null) return false;
                bool hasGlobal = CharStats.ScaledItemModifier(pc, weapon.Equip, Mod.ITEM_ADDEFFECT_TYPE) > 0;
                bool hasScripted = CharStats.ScaledItemModifier(pc, weapon.Equip, Mod.ITEM_ADDEFFECT_SCRIPTED) > 0;
                if (hasGlobal && hasScripted)
                    log_($"Item '{LookupItem(weapon.Equip.ItemId)?.Name}' has misconfigured additional effect data with both item script and add effect global configured");
                if (hasGlobal && AdditionalEffectAttack(attacker, defender, weapon.Equip, r, finalDamage) && r.AdditionalEffect != 0)
                {
                    if (r.AddEffectMessage == MsgAddEffectDamage && r.AddEffectParam < 0) r.AddEffectMessage = MsgAddEffectRecoversHp;
                    return true;
                }
                if (hasScripted && OnItemAdditionalEffect(attacker, defender, weapon.Equip, r, finalDamage) && r.AdditionalEffect != 0)
                {
                    if (r.AddEffectMessage == MsgAddEffectDamage && r.AddEffectParam < 0) r.AddEffectMessage = MsgAddEffectRecoversHp;
                    return true;
                }
                return false;
            }

            bool checkedPriorityWeaponAddEffect = false;
            if (pc != null && weapon?.Equip != null && CharStats.ScaledItemModifier(pc, weapon.Equip, Mod.ITEM_ADDEFFECT_PRIORITY) > 0)
            {
                if (CheckWeaponAdditionalEffect()) return;
                checkedPriorityWeaponAddEffect = true;
            }

            int enspell = attacker.GetMod(Mod.ENSPELL);
            int enspellChance = attacker.GetMod(Mod.ENSPELL_CHANCE);
            // (the chance is rolled only for an enspell up, as the C++ short-circuits)
            int runeCount = Runes(attacker).Count();
            if ((enspell > 0 && (enspellChance == 0 || enspellChance > MobRng.Next(100))) || runeCount > 0)
            {
                byte enspellByte = (byte)enspell;
                if (enspellByte == EnspellBloodWeapon && !IsUndead(defender))
                {
                    r.AdditionalEffect = ProcAddHpDrain;
                    r.AddEffectMessage = MsgAddEffectHpDrained;
                    // HP absorbed +2% a job point
                    int absorbed = r.Param;
                    if (pc != null) absorbed += (int)MathF.Floor(absorbed * 0.02f * JobPointUtil.Value(pc, JpBloodWeaponEffect));
                    r.AddEffectParam = BattleAddHp(attacker, absorbed);
                    if (pc != null) pc.UpdateMask |= CharStats.UpdateHp;
                }
                else if (runeCount > 0)
                {
                    // Rune Enhancement: the newest rune's element with one of each, else the strongest's
                    ushort highest = HighestRuneEffect(attacker);
                    ushort rune = attacker.StatusEffects.GetEffectsCount(highest) == 1
                        ? EffectInRange(attacker, EffectIgnis, EffectTenebrae, true).Id : highest;
                    int element = rune - EffectIgnis + 1;
                    r.AdditionalEffect = (byte)element;
                    r.AddEffectParam = CalculateEnspellDamage(attacker, defender, 4, element, weapon);
                    if (r.AddEffectParam < 0)
                    {
                        r.AddEffectParam = -r.AddEffectParam;
                        r.AddEffectMessage = MsgAddEffectRecoversHp;
                    }
                    else r.AddEffectMessage = MsgAddEffectAdditionalDamage;
                    BattleTakeDamage(defender, r.AddEffectParam, attacker, AttackTypeMagical, (byte)(DamageTypeElemental + element));
                }
                else if (enspellByte == EnspellAuspice && swing.First)
                {
                    r.AdditionalEffect = ProcAddLightDamage;
                    r.AddEffectMessage = MsgAddEffectAdditionalDamage;
                    r.AddEffectParam = CalculateEnspellDamage(attacker, defender, 2, 7, weapon);
                    if (r.AddEffectParam < 0)
                    {
                        r.AddEffectParam = -r.AddEffectParam;
                        r.AddEffectMessage = MsgAddEffectRecoversHp;
                    }
                    // GetEnspellDamageType(ENSPELL_AUSPICE) is DamageType::None
                    BattleTakeDamage(defender, r.AddEffectParam, attacker, AttackTypeMagical, 0);
                }
                else if (enspellByte <= EnspellIIDark)
                {
                    if (enspellByte > EnspellIDark && swing.First)
                    {
                        // tier II (Enlight II and Endark II not implemented; may vary)
                        r.AdditionalEffect = (byte)(enspellByte - 8);
                        r.AddEffectParam = CalculateEnspellDamage(attacker, defender, 2, enspellByte - 8, weapon);
                    }
                    else if (enspellByte <= EnspellIDark)
                    {
                        // tier I: Enlight and Endark are tier 3
                        r.AdditionalEffect = enspellByte;
                        r.AddEffectParam = CalculateEnspellDamage(attacker, defender, enspellByte >= EnspellILight ? 3 : 1, enspellByte, weapon);
                    }
                    if (r.AdditionalEffect != 0)
                    {
                        if (r.AddEffectParam < 0)
                        {
                            r.AddEffectParam = -r.AddEffectParam;
                            r.AddEffectMessage = MsgAddEffectRecoversHp;
                        }
                        else r.AddEffectMessage = MsgAddEffectAdditionalDamage;
                        int element = enspellByte > EnspellIDark ? enspellByte - 8 : enspellByte;
                        BattleTakeDamage(defender, r.AddEffectParam, attacker, AttackTypeMagical, (byte)(DamageTypeElemental + element));
                    }
                }
            }
            // the weapon's add effect, unless the priority check already ran
            else if (!checkedPriorityWeaponAddEffect && CheckWeaponAdditionalEffect())
            {
            }
            // a grip's (a sub item of no skill) when the main hand swung
            else if (pc?.Equipped[CharStats.SlotSub] != null && slot == CharStats.SlotMain)
            {
                if (pc.SubWeapon is { Equip: { } grip } sub && sub.Skill == Skill.None &&
                    CharStats.ScaledItemModifier(pc, grip, Mod.ITEM_ADDEFFECT_TYPE) > 0 &&
                    AdditionalEffectAttack(attacker, defender, grip, r, finalDamage) && r.AdditionalEffect != 0)
                {
                    if (r.AddEffectMessage == MsgAddEffectDamage && r.AddEffectParam < 0) r.AddEffectMessage = MsgAddEffectRecoversHp;
                }
            }
            else if (attacker is MobEntity aMob and not TrustEntity && aMob.GetMobMod(MobMod.AddEffect) > 0)
            {
                OnAdditionalEffect(aMob, defender, r, finalDamage);
                if (r.AddEffectMessage == MsgAddEffectDamage && r.AddEffectParam < 0) r.AddEffectMessage = MsgAddEffectRecoversHp;
            }
            else HandleDazeOnDefender(attacker, defender, r, finalDamage);
        }

        // the defender's daze, if the attacker's side put it there: Drain Daze drains HP, Aspir Daze MP, Haste Daze
        // grants Haste Samba's haste until the next round
        private void HandleDazeOnDefender(BattleEntity attacker, BattleEntity defender, ActionResult r, int finalDamage)
        {
            var fx = defender.StatusEffects;
            ushort daze = fx.HasStatusEffect(EffectDrainDaze) ? EffectDrainDaze
                        : fx.HasStatusEffect(EffectAspirDaze) ? EffectAspirDaze
                        : fx.HasStatusEffect(EffectHasteDaze) ? EffectHasteDaze
                        : EffectId.None;
            if (daze == EffectId.None) return;

            var pc = attacker as CharData;
            // TODO in the C++: what kind of value did this actually expect?
            int delay = MeleeFor().WeaponDelay(attacker, false) / 10;
            var dazeEffect = fx.GetStatusEffect(daze);
            uint attackerId = dazeEffect.SubId;
            ushort power = 0;
            if (pc?.Party != null)
            {
                foreach (var member in PartyWithTrusts(pc)) if (attackerId == member.Id) power = dazeEffect.Power;
            }
            else if (attacker is TrustEntity)
            {
                if (attacker.PMaster is CharData master)
                    foreach (var member in PartyWithTrusts(master)) if (attackerId == member.Id) power = dazeEffect.Power;
            }
            else if (attacker.PMaster == null)
            {
                if (attackerId == attacker.Id) power = dazeEffect.Power;
            }

            if (daze == EffectDrainDaze && power > 0)
            {
                ushort multiplier = (ushort)(3 + 5.5f * (power - 1));
                sbyte samba = (sbyte)(1 + MobRng.Next(delay * multiplier / 100 + 1));
                // 4% less a level the defender is above, up to 10
                sbyte lvlDiff = (sbyte)(defender.MLvl - attacker.MLvl);
                if (lvlDiff > 0)
                {
                    lvlDiff = Math.Min(lvlDiff, (sbyte)10);
                    samba -= (sbyte)Math.Ceiling(samba * lvlDiff * .04);
                }
                if (finalDamage <= 2) samba = 0;
                else if (samba > finalDamage / 2) samba = (sbyte)(finalDamage / 2);
                else if (samba < 0) samba = 0;

                r.AdditionalEffect = ProcAddHpDrain;
                r.AddEffectMessage = MsgAddEffectHpDrained;
                r.AddEffectParam = samba;
                // it only heals the attacker: no damage beyond the swing's
                BattleAddHp(attacker, samba);
                if (pc != null) pc.UpdateMask |= CharStats.UpdateHp;
            }
            else if (daze == EffectAspirDaze && power > 0 && defender.MaxMp > 0)
            {
                ushort multiplier = (ushort)(1 + 2 * (power - 1));
                sbyte samba = (sbyte)(1 + MobRng.Next(delay * multiplier / 100 + 1));
                if (finalDamage <= 2) samba = 0;
                else if (samba >= finalDamage / 4) samba = (sbyte)(finalDamage / 4);
                else if (samba < 0) samba = 0;

                r.AdditionalEffect = ProcAddMpDrain;
                r.AddEffectMessage = MsgAddEffectMpDrained;
                int mpDrained = BattleAddMp(defender, -samba);
                BattleAddMp(attacker, mpDrained);
                r.AddEffectParam = mpDrained;
                if (pc != null) pc.UpdateMask |= CharStats.UpdateHp;
            }
            else if (daze == EffectHasteDaze && power > 0)
            {
                r.AdditionalEffect = ProcAddHaste;
                // the ability haste comes from effects/haste_samba_haste.lua; the effect goes as the next round is built
                attacker.StatusEffects.AddStatusEffect(new StatusEffect(EffectHasteSambaHaste, 0, power, 0, 10_000));
            }
        }

        // ---- battleutils::CalculateEnspellDamage ---------------------------------------------------------------------

        // tier 1 the cast's precalculated ENSPELL_DMG, tier 2 one more a hit up to the enhancing skill's cap, tier 3
        // (Enlight / Endark) one less a hit until it wears, tier 4 a rune's from the weapon's DPS; then the enspell
        // damage % (the other hand's weapon's left out), Composure's x3 for a RDM's tier I / II, the resist, the
        // day and weather, MagicDmgTaken, Phalanx, One for All and stoneskin
        private int CalculateEnspellDamage(BattleEntity attacker, BattleEntity defender, int tier, int element, WeaponInfo weaponHit)
        {
            int damage = 0;
            var pc = attacker as CharData;
            var hitItem = weaponHit?.Equip;

            // the flat bonus: the attacker's less the other hand's weapon's
            int exclude = 0;
            if (pc != null)
                foreach (int slot in new[] { CharStats.SlotMain, CharStats.SlotSub })
                    if (pc.Equipped[slot] is { } eq && eq != hitItem) exclude += eq.GetModifier(Mod.ENSPELL_DMG_BONUS);
            int bonus = attacker.GetMod(Mod.ENSPELL_DMG_BONUS) - exclude;

            if (tier == 1)
            {
                // tier 1's damage was worked out at cast time
                damage = attacker.GetMod(Mod.ENSPELL_DMG) + bonus;
                if (pc != null) damage += pc.Merits?.Value(pc, MeritEnspellDamage) ?? 0;
            }
            else if (tier == 2)
            {
                // tier 2 grows a point a hit up to the cap
                int skill = Melee.GetSkill(attacker, Skill.EnhancingMagic);
                ushort cap = (ushort)(3 + 6 * skill / 100);
                if (skill > 200) cap = (ushort)(5 + 5 * skill / 100);
                cap *= 2;
                int current = attacker.GetMod(Mod.ENSPELL_DMG);
                if (current > cap)
                {
                    attacker.SetModifier(Mod.ENSPELL_DMG, (short)cap);
                    damage = cap;
                }
                else if (current == cap) damage = cap;
                else
                {
                    attacker.AddModifier(Mod.ENSPELL_DMG, 1);
                    damage = attacker.GetMod(Mod.ENSPELL_DMG) - 1;
                }
                damage += bonus;
                if (pc != null) damage += (pc.Merits?.Value(pc, MeritEnspellDamage) ?? 0) * 2;
            }
            else if (tier == 3)
            {
                // Enlight / Endark: a point less a hit, gone at 1
                damage = attacker.GetMod(Mod.ENSPELL_DMG);
                if (damage > 1) attacker.DelModifier(Mod.ENSPELL_DMG, 1);
                else attacker.StatusEffects.DelStatusEffect(element == 8 ? EffectEndark : EffectEnlight);
                damage += bonus;
            }
            else if (tier == 4)
            {
                // Rune Enhancement (ffxiah topic 56613): the main weapon's DPS, halved dual wielding, at most 21. A
                // mob's weapon and the unarmed one are no item_weapon row: no DPS (the C++'s 3 / 240 is for no
                // weapon at all, which never happens)
                double runeDps = pc?.MainWeapon is { Equip: not null } main ? main.Dps : 0.0;
                if (Melee.IsDualWielding(attacker)) runeDps /= 2;
                runeDps = Math.Min(21, runeDps);
                int runeBonus = attacker.StatusEffects.GetEffectsCount(HighestRuneEffect(attacker));
                double min = 0.0, max = 0.0;
                if (runeBonus == 1)
                {
                    min = Math.Floor(runeDps * 0.97);
                    max = Math.Floor(runeDps * 1.30);
                }
                else if (runeBonus == 2)
                {
                    min = Math.Floor(runeDps * 1.40);
                    max = Math.Floor(runeDps * 1.70);
                }
                else if (runeBonus == 3)
                {
                    min = Math.Floor(runeDps * 1.90);
                    max = Math.Floor(runeDps * 2.20);
                }
                // [min, max + 1)
                damage = max == 0.0 ? 0 : (int)(min + MobRng.NextDouble() * (max + 1 - min));
            }

            // the % bucket: non-weapon %, Composure, this hand's weapon %, applied once
            int weaponPct = 0, excludePct = 0;
            if (pc != null)
            {
                if (hitItem != null) weaponPct = hitItem.GetModifier(Mod.ENSPELL_DMG_PCT);
                foreach (int slot in new[] { CharStats.SlotMain, CharStats.SlotSub })
                    if (pc.Equipped[slot] is { } eq && eq != hitItem) excludePct += eq.GetModifier(Mod.ENSPELL_DMG_PCT);
            }
            int pctApplicable = attacker.GetMod(Mod.ENSPELL_DMG_PCT) - excludePct;
            int nonWeaponPct = Math.Max(pctApplicable - weaponPct, 0);
            float mult = 1.0f + nonWeaponPct / 100.0f;
            if (pc != null && pc.MJob == MobStats.JobRdm && attacker.StatusEffects.HasStatusEffect(EffectComposure) &&
                (tier == 1 || tier == 2) && element >= 1 && element <= 6)
                mult += 2.0f;
            mult += weaponPct / 100.0f;
            damage = (int)MathF.Floor(damage * mult);

            // the defender's resist: half, quarter, eighth or sixteenth by the element's MEVA
            float dBonus = 1.0f, resist = 1.0f;
            double half = defender.GetMod(ElementMeva[element - 1]) / 100.0;
            double quart = Math.Pow(half, 2), eighth = Math.Pow(half, 3), sixteenth = Math.Pow(half, 4);
            double resvar = MobRng.NextDouble();
            if (resvar <= sixteenth) resist = 0.0625f;
            else if (resvar <= eighth) resist = 0.125f;
            else if (resvar <= quart) resist = 0.25f;
            else if (resvar <= half) resist = 0.5f;

            // the day and weather: always with the element's obi on, else a third of the time; a mob's damage varies
            bool obiBonus = false;
            if (pc != null) obiBonus = pc.Equipped[SlotWaist]?.ItemId == 15435 + element - 1;
            else dBonus += MobRng.Next(100) / 1000.0f;
            byte day = DayElement();
            byte weak = EnspellWeakElement[element - 1];
            if (day == element && (obiBonus || MobRng.Next(100) < 33)) dBonus += 0.1f;
            else if (day == weak && (obiBonus || MobRng.Next(100) < 33)) dBonus -= 0.1f;
            ushort weather = BattleWeather(attacker);
            ushort strongWeather = PerpetuationWeather[element - 1], weakWeather = PerpetuationWeather[weak - 1];
            if (weather == strongWeather && (obiBonus || MobRng.Next(100) < 33)) dBonus += 0.1f;
            else if (weather == strongWeather + 1 && (obiBonus || MobRng.Next(100) < 33)) dBonus += 0.25f;
            else if (weather == weakWeather && (obiBonus || MobRng.Next(100) < 33)) dBonus -= 0.1f;
            else if (weather == weakWeather + 1 && (obiBonus || MobRng.Next(100) < 33)) dBonus -= 0.25f;

            damage = (int)(damage * resist);
            damage = (int)(damage * dBonus);
            damage = MagicDmgTaken(defender, damage, element);
            if (damage > 0)
            {
                damage = Math.Max(damage - defender.GetMod(Mod.PHALANX), 0);
                damage = HandleOneForAll(defender, damage);
                damage = defender.StatusEffects.HandleStoneskin(damage);
            }
            return Math.Clamp(damage, -99999, 99999);
        }

        // battleutils::MagicDmgTaken: Liement's absorb first, then the magic damage taken mods (MDT II past the
        // 50% cap, at most 87.5% in all), an absorb or a null by chance, and the damage cap
        private int MagicDmgTaken(BattleEntity defender, int damage, int element)
        {
            float liement = LiementAbsorb(defender, DamageTypeElemental + element);
            if (liement < 0) return (int)(damage * liement);

            float resist = Math.Max(1.0f + defender.GetMod(Mod.UDMGMAGIC) / 10000.0f, 0.0f);
            damage = (int)(damage * resist);
            resist = Math.Max(1.0f + defender.GetMod(Mod.DMGMAGIC) / 10000.0f + defender.GetMod(Mod.DMG) / 10000.0f, 0.5f);
            resist += defender.GetMod(Mod.DMGMAGIC_II) / 10000.0f;
            resist = Math.Max(resist, 0.125f);
            damage = (int)(damage * resist);

            if (MobRng.Next(100) < defender.GetMod(Mod.ABSORB_DMG_CHANCE) || MobRng.Next(100) < defender.GetMod(Mod.MAGIC_ABSORB) ||
                (element != 0 && MobRng.Next(100) < defender.GetMod(ElementAbsorb[element - 1])))
                damage = -damage;
            else if (MobRng.Next(100) < defender.GetMod(Mod.NULL_DAMAGE) || MobRng.Next(100) < defender.GetMod(Mod.NULL_MAGICAL_DAMAGE) ||
                     (element != 0 && MobRng.Next(100) < defender.GetMod(ElementNull[element - 1])))
                damage = 0;
            else damage = HandleSevereDamage(defender, damage, false);
            return MeleeFor().DamageCap(defender, damage);
        }

        // battleutils::HandleOneForAll
        private static int HandleOneForAll(BattleEntity defender, int damage)
        {
            if (damage > 0 && defender.StatusEffects.GetStatusEffect(EffectOneForAll) is { } ofa) damage = Math.Max(damage - ofa.Power, 0);
            return damage;
        }

        // ---- the scripts (luautils) ----------------------------------------------------------------------------------

        // the (subEffect, message, param) a script returns into the result; a non-number is none
        private void ReadAddEffectReturns(int top, ActionResult r)
        {
            // get<ActionProcAddEffect> and get<MsgBasic> are enums (LuaState.EnumOf), the param an int32
            r.AdditionalEffect = lua_.IsNumber(top + 1) ? (byte)lua_.ToEnum(top + 1) : ProcAddNone;
            r.AddEffectMessage = lua_.IsNumber(top + 2) ? (ushort)lua_.ToEnum(top + 2) : (ushort)0;
            r.AddEffectParam = lua_.IsNumber(top + 3) ? (int)lua_.ToIntegral(top + 3) : 0;
        }

        // luautils::additionalEffectAttack: xi.additionalEffect.attack(attacker, defender, baseAttackDamage, item), a
        // char's only; false when it could not run
        private bool AdditionalEffectAttack(BattleEntity attacker, BattleEntity defender, EquipmentInfo item, ActionResult r, int baseAttackDamage)
        {
            if (lua_ == null || attacker is not CharData || LookupItem(item.ItemId) is not { } luaItem) return false;
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.additionalEffect.attack");
                if (!lua_.IsFunction(-1)) return false;
                lua_.PushObject(attacker);
                lua_.PushObject(defender);
                lua_.Push((double)baseAttackDamage);
                lua_.PushObject(luaItem, ItemType);
                lua_.Call(4, 3);
                ReadAddEffectReturns(top, r);
                return true;
            }
            catch (LuaException e)
            {
                ScriptError("luautils::additionalEffectAttack", e);
                return false;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnItemAdditionalEffect: the item script's onItemAdditionalEffect(attacker, defender, damage, item)
        private bool OnItemAdditionalEffect(BattleEntity attacker, BattleEntity defender, EquipmentInfo item, ActionResult r, int baseAttackDamage)
        {
            if (lua_ == null || LookupItem(item.ItemId) is not { } luaItem) return false;
            string file = $"./scripts/items/{luaItem.Name}.lua";
            if (itemScripts_.Add(file)) scripts_.LoadLuaObjectFromFile(file);
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(file, "onItemAdditionalEffect");
                if (!lua_.IsFunction(-1)) return false;
                lua_.PushObject(attacker);
                lua_.PushObject(defender);
                lua_.Push((double)baseAttackDamage);
                lua_.PushObject(luaItem, ItemType);
                lua_.Call(4, 3);
                ReadAddEffectReturns(top, r);
                return true;
            }
            catch (LuaException e)
            {
                ScriptError("luautils::onItemAdditionalEffect", e);
                return false;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnAdditionalEffect: a mob's script's onAdditionalEffect(mob, target, damage), an automaton's
        // xi.pets.automaton.onAdditionalEffectAttack; other pets have none
        private void OnAdditionalEffect(MobEntity attacker, BattleEntity defender, ActionResult r, int damage)
        {
            if (lua_ == null) return;
            int top = lua_.Top;
            try
            {
                if (attacker is PetEntity pet)
                {
                    if (pet.PetType != PetType.Automaton) return;
                    lua_.GetPath("xi.pets.automaton.onAdditionalEffectAttack");
                }
                else PushMobFunction(attacker, "onAdditionalEffect");
                if (!lua_.IsFunction(-1)) return;
                lua_.PushObject(attacker);
                lua_.PushObject(defender);
                lua_.Push((double)damage);
                lua_.Call(3, 3);
                ReadAddEffectReturns(top, r);
            }
            catch (LuaException e)
            {
                ScriptError("luautils::onAdditionalEffect", e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }
    }
}
