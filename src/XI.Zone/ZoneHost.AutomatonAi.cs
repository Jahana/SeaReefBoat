using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // CAutomatonController: an automaton's combat tick, a decision every 3 s (less AUTO_DECISION_DELAY) driven by the
    // master's maneuvers -- the Valoredge frame's Shield Bash against a target readying something, the head's spells
    // (heal, enfeeble, nuke, status removal, enhance, each on its own cooldown from xi.pets.automaton.magicCooldowns),
    // a TP move (the most matching maneuvers by its script's onAutomatonAbilityCheck, then the highest skill; a
    // skillchain closer when AUTO_TP_EFFICIENCY waits on the master), the Sharpshot frame's ranged attack, then the
    // AUTOMATON_ATTACHMENT_CHECK listeners; standing back for an Animator II. automaton::LoadAutomatonSpellList /
    // CanUseSpell / CanUseEnfeeble / FindNaSpell (automaton_spells), LoadAutomatonAbilities (automaton_abilities'
    // scripts), luautils::OnAutomatonAbilityCheck / OnAutomatonAbility, puppetutils::TrySkillUP, and the
    // CAutomatonEntity::OnCastFinished / OnMobSkillFinished skill-ups and the master's base enmity.
    public sealed partial class ZoneHost
    {
        private const ushort BehaviorStandback = 0x002;
        private const ushort AutoRangedAbility = 1949, AutoShieldBashAbility = 1944;
        private const byte SubSkillAnimatorII = 11;
        private const byte FrameHarlequin = 0x20, FrameValoredge = 0x21, FrameSharpshot = 0x22, FrameStormwaker = 0x23;
        private const byte HeadHarlequin = 1, HeadValoredge = 2, HeadSharpshot = 3, HeadStormwaker = 4, HeadSoulsoother = 5, HeadSpiritreaver = 6;

        // xi::StatusEffect ids the controller reads
        private const ushort FxHaste = 33, FxStoneskin = 37, FxProtect = 40, FxShell = 41, FxRegen = 42, FxIntBoost = 84, FxPhalanx = 116,
            FxDia = 134, FxBio = 135, FxGeoRegen = 539, FxGeoHaste = 580, FxSilence = 6, FxMute = 29;

        // SpellID
        private const ushort SpCure = 1, SpCureII = 2, SpCureIII = 3, SpCureIV = 4, SpCureV = 5, SpCureVI = 6, SpDia = 23, SpDiaII = 24,
            SpProtect = 43, SpShell = 48, SpStoneskin = 54, SpSlow = 56, SpHaste = 57, SpParalyze = 58, SpSilence = 59, SpPhalanx = 106,
            SpRegen = 108, SpRegenII = 110, SpRegenIII = 111, SpProtectraV = 129, SpShellraV = 134, SpErase = 143, SpFire = 144,
            SpBlizzard = 149, SpAero = 154, SpStone = 159, SpThunder = 164, SpWater = 169, SpPoison = 220, SpPoisonII = 221, SpBio = 230,
            SpBioII = 231, SpDrain = 245, SpAspir = 247, SpAspirII = 248, SpBlind = 254, SpDispel = 260, SpAbsorbInt = 270,
            SpDreadSpikes = 277, SpAddle = 286, SpHasteII = 511;

        // AutomatonSpell
        private sealed class AutomatonSpell
        {
            public ushort SkillLevel;
            public byte Heads;
            public ushort Enfeeble;
            public uint Immunity;
            public readonly List<ushort> Removes = new List<ushort>();
        }

        private Dictionary<ushort, AutomatonSpell> autoSpells_;
        private readonly List<ushort> naSpells_ = new List<ushort>();
        private bool autoAbilitiesLoaded_;

        // automaton::LoadAutomatonSpellList: each spell's skill level, heads, the enfeeble it lays and the immunity
        // that stops it, the effects it removes (a byte each, low first; a spell that removes any is a -na spell)
        private Dictionary<ushort, AutomatonSpell> AutomatonSpells()
        {
            if (autoSpells_ != null) return autoSpells_;
            autoSpells_ = new Dictionary<ushort, AutomatonSpell>();
            var r = Exec("SELECT spellid, skilllevel, heads, enfeeble, immunity, removes FROM automaton_spells");
            while (r != null && r.Next())
            {
                var spell = new AutomatonSpell
                {
                    SkillLevel = r.Get<ushort>("skilllevel"),
                    Heads = r.Get<byte>("heads"),
                    Enfeeble = r.Get<ushort>("enfeeble"),
                    Immunity = r.Get<uint>("immunity"),
                };
                uint removes = r.Get<uint>("removes");
                while (removes > 0)
                {
                    spell.Removes.Add((ushort)(removes & 0xFF));
                    removes >>= 8;
                }
                ushort id = r.Get<ushort>("spellid");
                if (spell.Removes.Count > 0) naSpells_.Add(id);
                autoSpells_[id] = spell;
            }
            return autoSpells_;
        }

        // automaton::LoadAutomatonAbilities: their scripts
        private void LoadAutomatonAbilities()
        {
            if (autoAbilitiesLoaded_) return;
            autoAbilitiesLoaded_ = true;
            var r = Exec("SELECT abilityid, abilityname, reqframe, skilllevel FROM automaton_abilities");
            while (r != null && r.Next()) AbilityLoadScript($"./scripts/actions/abilities/pets/automaton/{r.Get<string>("abilityname")}.lua");
        }

        // automaton::CanUseSpell: the magic skill reaches it and the head may cast it (an unknown spell: neither)
        private bool AutomatonCanUseSpell(AutomatonEntity pet, ushort spellId)
        {
            var spell = AutomatonSpells().GetValueOrDefault(spellId);
            int skillLevel = spell?.SkillLevel ?? 0, heads = spell?.Heads ?? 0;
            return (pet.WorkingSkills[SkillAutomatonMagic] & 0x7FFF) >= skillLevel && (heads & 1 << (pet.Head - 1)) != 0;
        }

        // automaton::CanUseEnfeeble: the target lacks its effect and its immunity
        private bool AutomatonCanUseEnfeeble(BattleEntity target, ushort spellId)
        {
            var spell = AutomatonSpells().GetValueOrDefault(spellId);
            ushort enfeeble = spell?.Enfeeble ?? 0;
            uint immunity = spell?.Immunity ?? 0;
            return !target.StatusEffects.HasStatusEffect(enfeeble) && !(target is MobEntity m && (m.Immunity & immunity) != 0);
        }

        // automaton::FindNaSpell: the -na spell that removes it, else Erase for an erasable effect
        private ushort? AutomatonFindNaSpell(StatusEffect e)
        {
            var spells = AutomatonSpells();
            foreach (var id in naSpells_)
                if (spells[id].Removes.Contains(e.Id)) return id;
            return e.HasFlag(EffectFlag.Erasable) ? SpErase : null;
        }

        // ---- the controller's setup ----------------------------------------------------------------------------------

        // CAutomatonController::CAutomatonController: the cooldowns, standing back
        private void SetAutomatonController(AutomatonEntity pet)
        {
            AutomatonSetCooldowns(pet);
            if (AutomatonShouldStandBack(pet)) pet.Behavior |= BehaviorStandback;
        }

        // setCooldowns: the Sharpshot frame's ranged cooldown by head, the Harlequin and Stormwaker frames' magic, the
        // Valoredge frame's Shield Bash
        private void AutomatonSetCooldowns(AutomatonEntity pet)
        {
            switch (pet.Frame)
            {
                case FrameSharpshot:
                    pet.RangedCooldownMs = pet.Head switch { HeadSharpshot => 20000, HeadHarlequin => 25000, _ => 36000 };
                    break;
                case FrameHarlequin:
                case FrameStormwaker:
                    AutomatonSetMagicCooldowns(pet);
                    break;
                case FrameValoredge:
                    pet.ShieldBashCooldownMs = 180000;
                    break;
            }
        }

        // setMagicCooldowns: xi.pets.automaton.magicCooldowns[head]'s global / healing / enfeebling / elemental /
        // enhancing / statusRemoval seconds (0 when missing)
        private void AutomatonSetMagicCooldowns(AutomatonEntity pet)
        {
            if (lua_ == null) return;
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.pets.automaton.magicCooldowns");
                if (!lua_.IsTable(-1))
                {
                    log_("CAutomatonController::setMagicCooldowns() - Missing xi.pets.automaton.magicCooldowns");
                    return;
                }
                lua_.RawGetI(-1, pet.Head);
                if (!lua_.IsTable(-1))
                {
                    log_($"CAutomatonController::setMagicCooldowns() - Missing magic cooldowns for head {pet.Head}");
                    return;
                }
                long Category(string key)
                {
                    lua_.GetField(-1, key);
                    long seconds = lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : 0; // get<sol::optional<int32>>
                    lua_.Pop(1);
                    return seconds * 1000;
                }
                pet.MagicCooldownMs = Category("global");
                pet.HealCooldownMs = Category("healing");
                pet.EnfeebleCooldownMs = Category("enfeebling");
                pet.ElementalCooldownMs = Category("elemental");
                pet.EnhanceCooldownMs = Category("enhancing");
                pet.StatusCooldownMs = Category("statusRemoval");
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // shouldStandBack: with a master, only an Animator II in its ammo slot; without one (as written in C++), not a
        // Valoredge frame and a Sharpshot or later head
        private static bool AutomatonShouldStandBack(AutomatonEntity pet)
        {
            if (pet.PMaster != null)
            {
                if (pet.PMaster is CharData c && CharStats.WeaponIn(c, CharStats.SlotAmmo) is { Equip: not null } animator &&
                    animator.SubSkill == SubSkillAnimatorII)
                    return true;
            }
            else if (pet.Frame == FrameValoredge) return false;
            else if (pet.Head >= HeadSharpshot) return true;
            return false;
        }

        // ---- the combat tick -----------------------------------------------------------------------------------------

        private readonly struct Maneuvers
        {
            public readonly int Fire, Ice, Wind, Earth, Thunder, Water, Light, Dark;

            public Maneuvers(StatusEffectContainer fx)
            {
                Fire = fx.GetEffectsCount(EffectId.FireManeuver);
                Ice = fx.GetEffectsCount(EffectId.FireManeuver + 1);
                Wind = fx.GetEffectsCount(EffectId.FireManeuver + 2);
                Earth = fx.GetEffectsCount(EffectId.FireManeuver + 3);
                Thunder = fx.GetEffectsCount(EffectId.FireManeuver + 4);
                Water = fx.GetEffectsCount(EffectId.FireManeuver + 5);
                Light = fx.GetEffectsCount(EffectId.FireManeuver + 6);
                Dark = fx.GetEffectsCount(EffectId.FireManeuver + 7);
            }
        }

        // CAutomatonController::DoCombatTick: dies with its master; lets go of a target that is gone; a decision
        // every action cooldown (Shield Bash, a spell, a TP move, the ranged attack, the attachments), else the move
        private void AutomatonCombatTick(AutomatonEntity pet, long now)
        {
            if ((pet.PMaster == null || pet.PMaster.Hp <= 0) && pet.IsAlive)
            {
                PetDie(pet, now);
                return;
            }
            var target = pet.BattleTarget;
            if (PetTryDeaggro(pet, target))
            {
                AbortMagic(pet, now);
                MobDisengage(pet, now);
                return;
            }
            // Automatons only attempt actions in 3 second intervals (Reduced by the Tactical Processor)
            if (AutomatonTryAction(pet, target, now))
            {
                var maneuvers = new Maneuvers(pet.PMaster.StatusEffects);
                if (AutomatonTryShieldBash(pet, target, now))
                {
                    pet.LastShieldBashMs = now;
                    return;
                }
                if (AutomatonTrySpellcast(pet, target, maneuvers, now))
                {
                    pet.LastMagicMs = now;
                    return;
                }
                if (AutomatonTryTpMove(pet, target, now)) return;
                if (AutomatonTryRangedAttack(pet, target, now))
                {
                    pet.LastRangedMs = now;
                    return;
                }
                if (AutomatonTryAttachment(pet, target)) return;
            }
            AutomatonMove(pet, target, now);
        }

        // Move: no standing back once out past 15 (when it would stand back) or out of MP; then the chase, holding
        // position within 20 of the target while standing back (CMobController::ShouldCloseToTarget)
        private void AutomatonMove(AutomatonEntity pet, BattleEntity target, long now)
        {
            if (AutomatonShouldStandBack(pet) && Distance(pet, target) > 15.0f || pet.Mp < 8 && pet.MaxMp > 8)
                pet.Behavior &= unchecked((ushort)~BehaviorStandback);
            if (!pet.Engaged || pet.BattleTarget == null || !CanChangeState(pet)) return;
            if ((pet.Behavior & BehaviorStandback) != 0 && Distance(pet, target) < 20.0f)
            {
                FaceTarget(pet, target);
                return;
            }
            if (!MobFollowScriptedPath(pet, now)) MobMove(pet, target, now);
        }

        // TryAction: AUTOMATON_AI_TICK when the decision is due
        private bool AutomatonTryAction(AutomatonEntity pet, BattleEntity target, long now)
        {
            if (now > pet.LastActionMs + (pet.ActionCooldownMs - pet.GetMod(Mod.AUTO_DECISION_DELAY) * 10L))
            {
                pet.LastActionMs = now;
                TriggerListener(pet, "AUTOMATON_AI_TICK", pet, target);
                return true;
            }
            return false;
        }

        // CState::CanInterrupt of the target's current state: casting, readying a skill or a weapon skill, shooting,
        // using an ability
        private static bool StateInterruptible(BattleEntity t) => t.MagicState != null || t is MobEntity m && (m.SkillState != null || m.RangedState != null) ||
                                                                  t is TrustEntity tr && (tr.WsState != null || tr.AbilityState != null);

        // TryShieldBash
        private bool AutomatonTryShieldBash(AutomatonEntity pet, BattleEntity target, long now)
        {
            if (pet.ShieldBashCooldownMs > 0 && StateInterruptible(target) &&
                now > pet.LastShieldBashMs + (pet.ShieldBashCooldownMs - pet.GetMod(Mod.AUTO_SHIELD_BASH_DELAY) * 1000L))
                return AutomatonMobSkill(pet, target, AutoShieldBashAbility, now);
            return false;
        }

        // CanCastSpells (recasts and costs ignored): not silenced or muted, and free to change state
        private bool AutomatonCanCastSpells(AutomatonEntity pet) =>
            !pet.StatusEffects.HasStatusEffect(FxSilence, FxMute) && CanChangeState(pet);

        // Cast: a spell it can use and off recast, through CMobController::Cast
        private bool AutomatonCast(AutomatonEntity pet, BattleEntity target, ushort spellId, long now)
        {
            if (target == null || !AutomatonCanUseSpell(pet, spellId) || RecastsOf(pet).Has(RecastType.Magic, spellId)) return false;
            return MobCast(pet, target, spellId, now);
        }

        // MobSkill: off its ability recast, through CMobController::MobSkill
        private bool AutomatonMobSkill(AutomatonEntity pet, BattleEntity target, ushort skillId, long now)
        {
            if (RecastsOf(pet).Has(RecastType.Ability, skillId)) return false;
            return MobUseSkill(pet, target, skillId, null, now);
        }

        // TrySpellcast: the global magic cooldown (plus AUTO_MAGIC_COOLDOWN), then each head's order
        private bool AutomatonTrySpellcast(AutomatonEntity pet, BattleEntity target, Maneuvers m, long now)
        {
            if (pet.PMaster == null || pet.MagicCooldownMs == 0 ||
                now <= pet.LastMagicMs + (pet.MagicCooldownMs + pet.GetMod(Mod.AUTO_MAGIC_COOLDOWN) * 1000L) || !AutomatonCanCastSpells(pet))
                return false;

            bool Heal()
            {
                if (!AutomatonTryHeal(pet, target, m, now)) return false;
                pet.LastHealMs = now;
                return true;
            }
            bool Enfeeble()
            {
                if (!AutomatonTryEnfeeble(pet, target, m, now)) return false;
                pet.LastEnfeebleMs = now;
                return true;
            }
            bool Elemental()
            {
                if (!AutomatonTryElemental(pet, target, m, now)) return false;
                pet.LastElementalMs = now;
                return true;
            }
            bool Enhance()
            {
                if (!AutomatonTryEnhance(pet, target, now)) return false;
                pet.LastEnhanceMs = now;
                return true;
            }
            bool StatusRemoval()
            {
                if (!AutomatonTryStatusRemoval(pet, m, now)) return false;
                pet.LastStatusMs = now;
                return true;
            }

            switch (pet.Head)
            {
                case HeadValoredge:
                    return Heal();
                case HeadSharpshot:
                case HeadHarlequin:
                    if (m.Light != 0 && Heal()) return true; // Light -> Heal
                    return Enfeeble() || m.Light == 0 && Heal();
                case HeadStormwaker:
                {
                    bool lowHp = target.Hpp <= 30 && target.Hp <= 300;
                    if (lowHp && Elemental()) return true; // Mob low HP -> Nuke
                    if (m.Light != 0 && Heal()) return true; // Light -> Heal
                    if (!lowHp && m.Ice != 0 && Elemental()) return true; // Ice -> Nuke
                    return Enfeeble() || m.Light == 0 && Heal() || !lowHp && m.Ice == 0 && Elemental() || Enhance();
                }
                case HeadSoulsoother:
                    if (m.Light != 0 && Heal()) return true; // Light -> Heal
                    return StatusRemoval() || m.Light == 0 && Heal() || Enhance() || Enfeeble();
                case HeadSpiritreaver:
                    if (m.Ice != 0 && Elemental()) return true; // Ice -> Nuke
                    if (m.Dark != 0 && Enhance()) return true;
                    // Dark or self HPP/MPP < 75 -> Enfeeble
                    if ((m.Dark != 0 || pet.Hpp < 75 || pet.Mpp < 75) && Enfeeble()) return true;
                    return m.Ice == 0 && Elemental();
            }
            return false;
        }

        // the CE + VE a mob holds for an entity, or null when it has none
        private static int? EnmityOf(MobEntity mob, uint id) => mob.Enmity.Get(id) is { } e ? e.CE + e.VE : null;

        // TryHeal: the threshold by Light maneuvers (and AUTO_HEALING_THRESHOLD); with the hate on itself it cures itself
        // at 50% before its master, else its master first; Soulsoother with Light cures the party; the cure's tier by
        // the HP missing
        private bool AutomatonTryHeal(AutomatonEntity pet, BattleEntity target, Maneuvers m, long now)
        {
            var master = pet.PMaster;
            if (master == null || pet.HealCooldownMs == 0 ||
                now <= pet.LastHealMs + (pet.HealCooldownMs - pet.GetMod(Mod.AUTO_HEALING_DELAY) * 1000L))
                return false;

            float threshold = m.Light switch { 1 => 40, 2 => 50, 3 => 75, _ => 30 };
            threshold = Math.Clamp(threshold + pet.GetMod(Mod.AUTO_HEALING_THRESHOLD), 30.0f, 90.0f);
            BattleEntity castTarget = null;

            bool haveHate = false;
            var mob = target as MobEntity;
            if (mob != null)
            {
                int? masterEnmity = EnmityOf(mob, master.Id), selfEnmity = EnmityOf(mob, pet.Id);
                if (masterEnmity == null) haveHate = true;
                else if (selfEnmity == null) haveHate = false;
                else haveHate = selfEnmity > masterEnmity;
            }

            // Prioritize hate
            if (haveHate)
            {
                if (pet.Hpp <= 50) castTarget = pet; // Automaton only heals itself when <= 50%
                else if (master.Hpp <= threshold && Distance(pet, master) < 20) castTarget = master;
            }
            else
            {
                if (master.Hpp <= threshold) castTarget = master;
                else if (pet.Hpp <= 50) castTarget = pet; // Automaton only heals itself when <= 50%
            }

            // Light + Soulsoother head -> Heal party
            if (m.Light != 0 && castTarget == null && pet.Head == HeadSoulsoother && master is CharData mc && mc.Party != null)
            {
                if (mob != null)
                {
                    int highestEnmity = 0;
                    foreach (var member in PartyWithTrusts(mc))
                    {
                        if (member.Id == master.Id) continue;
                        if (EnmityOf(mob, member.Id) is int enmity && highestEnmity < enmity && member.Hpp <= threshold && Distance(pet, master) < 20)
                        {
                            highestEnmity = enmity;
                            castTarget = member;
                        }
                    }
                }
                else
                {
                    foreach (var member in PartyWithTrusts(mc))
                        if (member.Id != master.Id && Distance(pet, master) < 20 && member.Hpp <= threshold) castTarget = member;
                }
            }

            // This might be wrong
            if (castTarget == null) return false;
            int missing = castTarget.MaxHp - castTarget.Hp;
            return missing > 850 && AutomatonCast(pet, castTarget, SpCureVI, now) ||
                   missing > 600 && AutomatonCast(pet, castTarget, SpCureV, now) ||
                   missing > 350 && AutomatonCast(pet, castTarget, SpCureIV, now) ||
                   missing > 190 && AutomatonCast(pet, castTarget, SpCureIII, now) ||
                   missing > 120 && AutomatonCast(pet, castTarget, SpCureII, now) ||
                   AutomatonCast(pet, castTarget, SpCure, now);
        }

        // TryElemental: the tier by the target's HP and its own MP; AUTO_SCAN_RESISTS orders by the target's resistance
        // ranks; Spiritreaver puts the maneuvers' elements first; else thunder, blizzard, fire, aero, water, stone
        private bool AutomatonTryElemental(AutomatonEntity pet, BattleEntity target, Maneuvers m, long now)
        {
            if (pet.PMaster == null || pet.ElementalCooldownMs == 0 || now <= pet.LastElementalMs + pet.ElementalCooldownMs) return false;

            var castPriority = new List<ushort>();
            var defaultPriority = new List<ushort>();
            int tier = 4, hp = target.Hp, selfMp = pet.Mp; // Shortcut for wasting less time
            if (selfMp < 4) return false;
            if (hp <= 50 || selfMp < 16) tier = 0;
            else if (hp <= 150 || selfMp < 40) tier = 1;
            else if (hp <= 200 || selfMp < 88) tier = 2;
            else if (hp <= 600 || selfMp < 156) tier = 3;

            if (pet.GetMod(Mod.AUTO_SCAN_RESISTS) != 0)
            {
                var resList = new List<(ushort Spell, int Res)>
                {
                    (SpFire, target.GetMod(Mod.FIRE_RES_RANK)), (SpBlizzard, target.GetMod(Mod.ICE_RES_RANK)),
                    (SpAero, target.GetMod(Mod.WIND_RES_RANK)), (SpStone, target.GetMod(Mod.EARTH_RES_RANK)),
                    (SpThunder, target.GetMod(Mod.THUNDER_RES_RANK)), (SpWater, target.GetMod(Mod.WATER_RES_RANK)),
                };
                // stable_sort
                foreach (var (spell, _) in System.Linq.Enumerable.OrderBy(resList, r => r.Res)) castPriority.Add(spell);
            }
            else if (pet.Head == HeadSpiritreaver)
            {
                foreach (var (count, spell) in new[] { (m.Thunder, SpThunder), (m.Ice, SpBlizzard), (m.Fire, SpFire), (m.Wind, SpAero),
                                                       (m.Water, SpWater), (m.Earth, SpStone) })
                    (count != 0 ? castPriority : defaultPriority).Add(spell);
            }
            else defaultPriority.AddRange(new[] { SpThunder, SpBlizzard, SpFire, SpAero, SpWater, SpStone });

            for (int i = tier; i >= 0; --i)
            {
                foreach (var id in castPriority)
                    if (AutomatonCast(pet, target, (ushort)(id + i), now)) return true;
                foreach (var id in defaultPriority)
                    if (AutomatonCast(pet, target, (ushort)(id + i), now)) return true;
            }
            return false;
        }

        // TryEnfeeble: each head's list, a spell first when its maneuver is up (Stormwaker dispels a dispellable
        // effect first); a spell only when the target lacks its effect and immunity
        private bool AutomatonTryEnfeeble(AutomatonEntity pet, BattleEntity target, Maneuvers m, long now)
        {
            if (pet.PMaster == null || pet.EnfeebleCooldownMs == 0 || now <= pet.LastEnfeebleMs + pet.EnfeebleCooldownMs) return false;

            var castPriority = new List<ushort>();
            var defaultPriority = new List<ushort>();
            void By(bool prioritized, params ushort[] spells) => (prioritized ? castPriority : defaultPriority).AddRange(spells);
            bool dia = target.StatusEffects.HasStatusEffect(FxDia), bio = target.StatusEffects.HasStatusEffect(FxBio);

            switch (pet.Head)
            {
                case HeadSpiritreaver:
                    if (pet.Mpp < 75 && target.Mp > 0) castPriority.AddRange(new[] { SpAspirII, SpAspir }); // MPP < 75 -> Aspir
                    if (pet.Hpp < 75 && target is not MobEntity { Ecosystem: Ecosystem.Undead }) castPriority.Add(SpDrain); // HPP <= 75 -> Drain
                    if (m.Dark != 0) // Dark -> Access to Enfeebles
                    {
                        if (!pet.StatusEffects.HasStatusEffect(FxIntBoost)) defaultPriority.Add(SpAbsorbInt); // Use it ASAP
                        // Not prioritizable since it requires 1 Dark to access Enfeebles and requires 2 of another element to prioritize another
                        defaultPriority.Add(SpBlind);
                        if (!dia) defaultPriority.Add(SpBioII);
                        if (!bio) By(m.Light >= 2, SpDiaII); // 2 Light -> Dia
                        if (!dia) defaultPriority.Add(SpBio);
                        if (!bio) By(m.Light >= 2, SpDia);
                        By(m.Water >= 2, SpPoisonII, SpPoison); // 2 Water -> Poison
                        By(m.Wind >= 2, SpSilence); // 2 Wind -> Silence
                        By(m.Earth >= 2, SpSlow); // 2 Earth -> Slow
                        By(m.Ice >= 2, SpParalyze); // 2 Ice -> Paralyze
                        By(m.Fire >= 2, SpAddle); // 2 Fire -> Addle
                    }
                    break;
                case HeadSoulsoother:
                    By(m.Earth != 0, SpSlow); // Earth -> Slow
                    By(m.Water != 0, SpPoisonII, SpPoison);
                    // Dark -> Blind > Bio
                    By(m.Dark != 0, SpBlind);
                    if (!dia) By(m.Dark != 0, SpBioII);
                    if (!bio) By(m.Light != 0, SpDiaII); // Light -> Dia
                    if (!dia) By(m.Dark != 0, SpBio);
                    if (!bio) By(m.Light != 0, SpDia);
                    By(m.Wind != 0, SpSilence); // Wind -> Silence
                    By(m.Ice != 0, SpParalyze); // Ice -> Paralyze
                    By(m.Fire != 0, SpAddle); // Fire -> Addle
                    break;
                default:
                    if (pet.Head == HeadStormwaker)
                    {
                        bool dispel = false;
                        foreach (var e in target.StatusEffects.All)
                            if (e.DurationMs > 0 && e.HasFlag(EffectFlag.Dispelable))
                            {
                                dispel = true;
                                break;
                            }
                        if (dispel) castPriority.Add(SpDispel);
                    }
                    if (!dia) By(m.Dark != 0, SpBioII); // Dark -> Bio
                    if (!bio) By(m.Light != 0, SpDiaII);
                    if (!dia) By(m.Dark != 0, SpBio);
                    if (!bio) By(m.Light != 0, SpDia);
                    By(m.Water != 0, SpPoisonII, SpPoison); // Water -> Poison
                    By(m.Wind != 0, SpSilence); // Wind -> Silence
                    By(m.Earth != 0, SpSlow); // Earth -> Slow
                    By(m.Dark != 0, SpBlind); // Dark -> Blind
                    By(m.Ice != 0, SpParalyze); // Ice -> Paralyze
                    By(m.Fire != 0, SpAddle); // Fire -> Addle
                    break;
            }

            foreach (var id in castPriority)
                if (AutomatonCanUseEnfeeble(target, id) && AutomatonCast(pet, target, id, now)) return true;
            foreach (var id in defaultPriority)
                if (AutomatonCanUseEnfeeble(target, id) && AutomatonCast(pet, target, id, now)) return true;
            return false;
        }

        // the -na spells for an entity's timed effects, in their order
        private List<ushort> NaSpellsFor(BattleEntity e)
        {
            var list = new List<ushort>();
            foreach (var effect in e.StatusEffects.All)
                if (effect.DurationMs > 0 && AutomatonFindNaSpell(effect) is ushort id) list.Add(id);
            return list;
        }

        // TryStatusRemoval: the master's effects, then its own; Water + Soulsoother head the party's
        private bool AutomatonTryStatusRemoval(AutomatonEntity pet, Maneuvers m, long now)
        {
            var master = pet.PMaster;
            if (master == null || pet.StatusCooldownMs == 0 || now <= pet.LastStatusMs + pet.StatusCooldownMs) return false;
            foreach (var id in NaSpellsFor(master))
                if (AutomatonCast(pet, master, id, now)) return true;
            foreach (var id in NaSpellsFor(pet))
                if (AutomatonCast(pet, pet, id, now)) return true;
            // Water + Soulsoother head -> Remove party's statuses
            if (m.Water != 0 && pet.Head == HeadSoulsoother && master is CharData mc && mc.Party != null)
            {
                foreach (var member in mc.Party.Members)
                {
                    if (member.Id == master.Id) continue;
                    foreach (var id in NaSpellsFor(member))
                        if (AutomatonCast(pet, member, id, now)) return true;
                }
            }
            return false;
        }

        // TryEnhance: Spiritreaver's Dread Spikes; else Regen on whoever holds the most hate, Protect / Shell / Haste /
        // Stoneskin / Phalanx on the master in a fight (within 20), then itself, then the party; Protectra / Shellra V
        // with 4 members missing them
        private bool AutomatonTryEnhance(AutomatonEntity pet, BattleEntity target, long now)
        {
            var master = pet.PMaster;
            if (master == null || pet.EnhanceCooldownMs == 0 || now <= pet.LastEnhanceMs + pet.EnhanceCooldownMs) return false;
            if (pet.Head == HeadSpiritreaver) return AutomatonCast(pet, pet, SpDreadSpikes, now);

            int highestEnmity = 0;
            BattleEntity regenTarget = null, protectTarget = null, shellTarget = null, hasteTarget = null, stoneskinTarget = null, phalanxTarget = null;
            bool protect = false, shell = false, haste = false, stoneskin = false, phalanx = false;
            int protectCount = 0, shellCount = 0;
            bool isEngaged = false;
            var mob = target as MobEntity;

            void Scan(BattleEntity e, bool counts, bool skinAndPhalanx)
            {
                foreach (var fx in e.StatusEffects.All)
                {
                    if (fx.DurationMs <= 0) continue;
                    if (fx.Id == FxProtect)
                    {
                        protect = true;
                        if (counts) ++protectCount;
                    }
                    if (fx.Id == FxShell)
                    {
                        shell = true;
                        if (counts) ++shellCount;
                    }
                    if (fx.Id == FxHaste || fx.Id == FxGeoHaste) haste = true;
                    if (skinAndPhalanx && fx.Id == FxStoneskin) stoneskin = true;
                    if (skinAndPhalanx && fx.Id == FxPhalanx) phalanx = true;
                }
            }

            if (Distance(pet, master) < 20)
            {
                if (mob != null)
                {
                    if (EnmityOf(mob, master.Id) is int enmity)
                    {
                        isEngaged = true;
                        if (highestEnmity < enmity)
                        {
                            highestEnmity = enmity;
                            regenTarget = master;
                        }
                    }
                    else isEngaged = true; // Assume everyone is engaged if the target isn't a mob
                }
                Scan(master, true, true);
                if (isEngaged)
                {
                    if (!protect) protectTarget = master;
                    if (!shell) shellTarget = master;
                    if (!haste) hasteTarget = master;
                    if (!stoneskin) stoneskinTarget = master;
                    if (!phalanx) phalanxTarget = master;
                }
            }

            protect = shell = haste = stoneskin = phalanx = false;
            if (mob != null && EnmityOf(mob, pet.Id) is int selfEnmity && highestEnmity < selfEnmity)
            {
                highestEnmity = selfEnmity;
                regenTarget = pet;
            }
            Scan(pet, false, false);
            if (protectTarget == null && !protect) protectTarget = pet;
            if (shellTarget == null && !shell) shellTarget = pet;
            if (hasteTarget == null && !haste) hasteTarget = pet;

            int members = 0;
            // Unknown whether it only applies buffs to other members if they have hate or if the Soulsoother head is needed
            if (master is CharData mc && mc.Party != null)
            {
                members = mc.Party.Members.Count;
                foreach (var member in PartyWithTrusts(mc))
                {
                    if (member.Id == master.Id || Distance(pet, member) >= 20) continue;
                    protect = shell = haste = false;
                    isEngaged = false;
                    if (mob != null)
                    {
                        if (EnmityOf(mob, member.Id) is int enmity)
                        {
                            isEngaged = true;
                            if (highestEnmity < enmity)
                            {
                                highestEnmity = enmity;
                                regenTarget = member;
                            }
                        }
                    }
                    else isEngaged = true; // Assume everyone is engaged if the target isn't a mob
                    Scan(member, true, false);
                    if (isEngaged)
                    {
                        if (protectTarget == null && !protect) protectTarget = member;
                        if (shellTarget == null && !shell) shellTarget = member;
                        if (hasteTarget == null && !haste) hasteTarget = member;
                    }
                }
            }

            // No info on how this spell worked (size_t: a negative difference wraps past 4)
            if ((uint)(members - protectCount) >= 4) AutomatonCast(pet, pet, SpProtectraV, now);
            if ((uint)(members - shellCount) >= 4) AutomatonCast(pet, pet, SpShellraV, now);

            if (regenTarget != null && !regenTarget.StatusEffects.HasStatusEffect(FxRegen) && !regenTarget.StatusEffects.HasStatusEffect(FxGeoRegen) &&
                (AutomatonCast(pet, regenTarget, SpRegenIII, now) || AutomatonCast(pet, regenTarget, SpRegenII, now) ||
                 AutomatonCast(pet, regenTarget, SpRegen, now)))
                return true;
            if (protectTarget != null)
                for (int i = 4; i >= 0; i--)
                    if (AutomatonCast(pet, protectTarget, (ushort)(SpProtect + i), now)) return true;
            if (shellTarget != null)
                for (int i = 4; i >= 0; i--)
                    if (AutomatonCast(pet, shellTarget, (ushort)(SpShell + i), now)) return true;
            if (hasteTarget != null && (AutomatonCast(pet, hasteTarget, SpHasteII, now) || AutomatonCast(pet, hasteTarget, SpHaste, now))) return true;
            if (stoneskinTarget != null && AutomatonCast(pet, stoneskinTarget, SpStoneskin, now)) return true;
            if (phalanxTarget != null && AutomatonCast(pet, phalanxTarget, SpPhalanx, now)) return true;
            return false;
        }

        // TryTPMove: at 1000 TP, the frame list's moves its melee (Sharpshot: ranged) skill reaches and in range; with
        // AUTO_TP_EFFICIENCY a closer for an open skillchain (waiting out its first 3 s), else, unless waiting on the
        // master's TP, the move with the most matching maneuvers, then the highest skill, then the highest id
        private bool AutomatonTryTpMove(AutomatonEntity pet, BattleEntity target, long now)
        {
            if (pet.Tp < 1000) return false;
            LoadAutomatonAbilities();
            int skillType = pet.Frame == FrameSharpshot ? SkillAutomatonRanged : SkillAutomatonMelee;
            int skill = pet.WorkingSkills[skillType] & 0x7FFF;
            var validSkills = new List<MobSkill>();
            foreach (var id in GetMobSkillList(pet.MobSkillList))
                if (GetMobSkill(id) is { } s && skill >= s.Param && s.Param != -1 && Distance(pet, target) < s.Radius) validSkills.Add(s);

            int currentSkill = -1, currentManeuvers = -1;
            MobSkill chosen = null;
            // Follows most matching maneuvers, then highest skill requirement, then highest skill ID.
            bool HasSkillPriority(MobSkill s, int maneuvers)
            {
                if (maneuvers != currentManeuvers) return maneuvers > currentManeuvers;
                if (s.Param != currentSkill) return s.Param > currentSkill;
                return chosen != null && s.Id > chosen.Id;
            }

            bool attemptChain = pet.GetMod(Mod.AUTO_TP_EFFICIENCY) != 0;
            var sc = target.StatusEffects.GetStatusEffect(EffectSkillchain, 0);
            // Skillchain has been started, wait 3 seconds until skillchain window is open before executing weaponskill.
            if (attemptChain && sc != null && sc.StartMs + 3000 >= now) return false;
            if (attemptChain && sc != null)
            {
                var resonance = ResonanceOf(sc);
                foreach (var s in validSkills)
                {
                    if (!HasSkillPriority(s, 1)) continue;
                    if (FormSkillchain(resonance, new List<byte> { s.PrimarySc, s.SecondarySc, s.TertiarySc }) != 0)
                    {
                        currentManeuvers = 1;
                        currentSkill = s.Param;
                        chosen = s;
                    }
                }
            }
            if (!attemptChain || currentManeuvers == -1 && pet.PMaster != null && pet.PMaster.Tp < pet.GetMod(Mod.AUTO_TP_EFFICIENCY))
            {
                foreach (var s in validSkills)
                {
                    int maneuvers = OnAutomatonAbilityCheck(target, pet, s);
                    if (maneuvers > -1 && HasSkillPriority(s, maneuvers))
                    {
                        currentManeuvers = maneuvers;
                        currentSkill = s.Param;
                        chosen = s;
                    }
                }
            }
            // No WS was chosen (waiting on master's TP to skillchain probably)
            if (currentManeuvers == -1) return false;
            return chosen != null && AutomatonMobSkill(pet, target, chosen.Id, now);
        }

        // TryRangedAttack: the Sharpshot frame's shot every ranged cooldown (less AUTO_RANGED_DELAY, at least 5 s with
        // the Sharpshot head, else 10 s)
        private bool AutomatonTryRangedAttack(AutomatonEntity pet, BattleEntity target, long now)
        {
            if (pet.Frame != FrameSharpshot) return false;
            long minDelay = pet.Head == HeadSharpshot ? 5000 : 10000;
            long attackTime = pet.RangedCooldownMs - pet.GetMod(Mod.AUTO_RANGED_DELAY) * 1000L;
            if (pet.RangedCooldownMs > 0 && now > pet.LastRangedMs + Math.Max(attackTime, minDelay))
                return AutomatonMobSkill(pet, target, AutoRangedAbility, now);
            return false;
        }

        // TryAttachment: the AUTOMATON_ATTACHMENT_CHECK listeners, when free to act
        private bool AutomatonTryAttachment(AutomatonEntity pet, BattleEntity target)
        {
            if (!CanChangeState(pet)) return false;
            TriggerListener(pet, "AUTOMATON_ATTACHMENT_CHECK", pet, target);
            return false;
        }

        // CAutomatonController::Disengage: standing back again
        private static void AutomatonDisengaged(AutomatonEntity pet)
        {
            if (AutomatonShouldStandBack(pet)) pet.Behavior |= BehaviorStandback;
        }

        // ---- the scripts ---------------------------------------------------------------------------------------------

        private static string AutomatonAbilityScript(MobSkill skill) => $"./scripts/actions/abilities/pets/automaton/{skill.Name}.lua";

        // luautils::OnAutomatonAbilityCheck: the maneuvers the move matches (no function: 1; an error: 1; not a
        // number: -5)
        private int OnAutomatonAbilityCheck(BattleEntity target, AutomatonEntity pet, MobSkill skill)
        {
            if (lua_ == null) return 1;
            int top = lua_.Top;
            try
            {
                AbilityLoadScript(AutomatonAbilityScript(skill));
                scripts_.PushFileFunction(AutomatonAbilityScript(skill), "onAutomatonAbilityCheck");
                if (lua_.IsNil(-1)) return 1;
                lua_.Push(target);
                lua_.Push(pet);
                lua_.Push(skill);
                lua_.Call(3, 1);
                return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : -5;
            }
            catch (LuaException e)
            {
                log_($"luautils::onAutomatonAbilityCheck: {e.Message}");
                return 1;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnAutomatonAbility: the move's script, whose number is the damage
        private int OnAutomatonAbility(BattleEntity target, AutomatonEntity pet, MobSkill skill, BattleEntity master, ActionPacket action)
        {
            if (lua_ == null) return 0;
            int top = lua_.Top;
            try
            {
                AbilityLoadScript(AutomatonAbilityScript(skill));
                scripts_.PushFileFunction(AutomatonAbilityScript(skill), "onAutomatonAbility");
                if (lua_.IsNil(-1)) return 0;
                lua_.Push(target);
                lua_.Push(pet);
                lua_.Push(skill);
                if (master != null) lua_.Push(master);
                else lua_.PushNil();
                lua_.Push(action);
                lua_.Call(5, 1);
                return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : 0;
            }
            catch (LuaException e)
            {
                log_($"luautils::onAutomatonAbility: {e.Message}");
                return 0;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // ---- the skill-ups and the master's enmity -------------------------------------------------------------------

        // puppetutils::TrySkillUP: the master's automaton skill, rolled as a char's combat skill; a level gained
        // raises both and resends the PUP job packets
        private void AutomatonTrySkillUp(AutomatonEntity pet, int skillId, int level)
        {
            if (pet.PMaster is not CharData c)
            {
                log_("puppetutils::TrySkillUP() - PMaster was null, or was not a player.");
                return;
            }
            if (AutomatonSkillCap(c, skillId, pet.MLvl) == 0 || (pet.WorkingSkills[skillId] & 0x8000) != 0) return;
            int curSkill = c.RealSkills[skillId];
            int maxSkill = AutomatonSkillCap(c, skillId, Math.Min((int)pet.MLvl, level));
            short diff = (short)(maxSkill - curSkill / 10);
            var cfg = ChatCfg;
            double chance = diff / 5.0 + cfg.SkillupChanceMultiplier * (2.0 - Math.Log10(1.0 + curSkill / 100));
            double random = Random01();
            chance = Math.Min(chance, 0.5);
            chance *= (100.0f + pet.GetMod(Mod.COMBAT_SKILLUP_RATE)) / 100.0f;
            if (diff <= 0 || random >= chance) return;

            int amount = 1, tier = Math.Min(1 + diff / 5, 5);
            for (int i = 0; i < 4; ++i) // 1 + 4 possible additional ones (maximum 5)
            {
                random = Random01();
                double extra = tier switch { 5 => 0.900, 4 => 0.700, 3 => 0.500, 2 => 0.300, 1 => 0.200, _ => 0.000 };
                if (extra < random || amount == 5) break;
                tier -= 1;
                amount += 1;
            }
            maxSkill *= 10;
            // Do skill amount multiplier (Will only be applied if default setting is changed)
            if (cfg.SkillupAmountMultiplier > 1)
                amount = Math.Min((int)(byte)(amount + (byte)(amount * cfg.SkillupAmountMultiplier)), 9);
            if (amount + curSkill >= maxSkill)
            {
                amount = maxSkill - curSkill;
                pet.WorkingSkills[skillId] |= 0x8000;
            }
            c.RealSkills[skillId] += (ushort)amount;
            PushTo(c, SelfPackets.BattleMessage2(pet, pet, skillId, amount, MsgSkillGain));
            if (curSkill / 10 < (curSkill + amount) / 10) // if gone up a level
            {
                c.WorkingSkills[skillId] += 1;
                pet.WorkingSkills[skillId] += 1;
                if (skillId == SkillAutomatonMagic)
                {
                    ushort ama = pet.WorkingSkills[SkillAutomatonMagic];
                    for (int i = SkillHealing; i <= Skill.DarkMagic; i++)
                        if (i != Skill.DivineMagic) pet.WorkingSkills[i] = ama;
                }
                if (SessionOf(c) is { } s) SendExtendedJobPackets(s, c);
                PushTo(c, SelfPackets.BattleMessage2(pet, pet, skillId, (curSkill + amount) / 10, MsgSkillLevelUp));
            }
            SaveCharSkill(c, skillId);
        }

        // the master's base enmity on a mob of another allegiance the automaton acted on
        private static void AutomatonMasterEnmity(AutomatonEntity pet, BattleEntity target)
        {
            if (target is MobEntity mob && mob.Allegiance != CharData.AllegiancePlayer && pet.PMaster is CharData master)
                mob.Enmity.AddBaseEnmity(master);
        }

        // CAutomatonEntity::OnCastFinished: a spell that took effect gives an automaton magic skill-up try and the
        // master's base enmity on the mob (the recast is CBattleEntity's)
        private void AutomatonOnCastFinished(AutomatonEntity pet, Spell spell, BattleEntity target)
        {
            if (!spell.TookEffect) return;
            AutomatonTrySkillUp(pet, SkillAutomatonMagic, target.MLvl);
            AutomatonMasterEnmity(pet, target);
        }

        // CAutomatonEntity::OnMobSkillFinished: the master's base enmity on the mob; the ranged attack's automaton
        // ranged skill-up try, or a frame move's (Sharpshot: ranged, else melee) when it did not miss
        private void AutomatonOnMobSkillFinished(AutomatonEntity pet, MobSkill skill, BattleEntity target)
        {
            AutomatonMasterEnmity(pet, target);
            // Ranged attack skill up
            if (skill.Id == AutoRangedAbility && !skill.HasMissMsg) AutomatonTrySkillUp(pet, SkillAutomatonRanged, target.MLvl);
            // Weaponskill skill up - TODO: This should be able to happen on every hit of the weaponskill, but the fix isn't that simple at the moment.
            else if (target != null && !skill.HasMissMsg && GetMobSkillList(pet.MobSkillList).Contains(skill.Id))
                AutomatonTrySkillUp(pet, pet.Frame == FrameSharpshot ? SkillAutomatonRanged : SkillAutomatonMelee, target.MLvl);
        }
    }
}
