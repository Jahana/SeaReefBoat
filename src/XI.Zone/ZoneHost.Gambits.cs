using System;
using System.Collections.Generic;
using System.Linq;
using XI.Map;

namespace XI.Zone
{
    // CGambitsContainer::Tick and CheckTrigger: every 2-3 s (offset 100 ms per party place) a trust that is not busy
    // runs its gambits in order: the targets its selector gives (itself, its target, the master, the party within 15
    // yalms by job or enmity, the dead), the first whose predicate groups all hold, then the gambit's actions (one spell
    // per gambit, through CTrustController::Cast: the trusts casting alike hold back), its retry delay. The predicates
    // are all ported (the runes through getAllRuneEffects' list, Val's Uriel check over the master's notoriety
    // container). Reactions: the spells (SPECIFIC, HIGHEST, RANDOM, MB_ELEMENT, DEF_BAR_ELEMENT; LOWEST
    // never resolves, as the C++), the job abilities (SPECIFIC, HIGHEST_WALTZ, BEST_SAMBA, RUNE_DAY), the ranged
    // attack, a specific mob skill and the animation strings; the TP skills go first (ZoneHost.TrustSkills.cs). Not
    // ported: the spell selectors that need the weakness, day and indi pickers (BEST_INDI, ENTRUSTED,
    // BEST_AGAINST_TARGET, STORM_DAY, HELIX_DAY, the *_MOB_WEAKNESS ones): those actions never resolve.
    public sealed partial class ZoneHost
    {
        // G_TARGET
        private const ushort GTargetSelf = 0, GTargetParty = 1, GTargetTarget = 2, GTargetMaster = 3, GTargetTank = 4, GTargetMelee = 5,
            GTargetRanged = 6, GTargetCaster = 7, GTargetTopEnmity = 8, GTargetCurilla = 9, GTargetPartyDead = 10,
            GTargetTriggerSelfActionTarget = 12, GTargetTriggerTargetActionSelf = 13;
        // G_CONDITION
        private const ushort GCondAlways = 0, GCondHppLt = 1, GCondHppGte = 2, GCondMppLt = 3, GCondMppGte = 4, GCondTpLt = 5,
            GCondTpGte = 6, GCondLvlLt = 7, GCondLvlGte = 8, GCondStatus = 9, GCondNotStatus = 10, GCondStatusFlag = 11,
            GCondHasTopEnmity = 12, GCondNotHasTopEnmity = 13, GCondScAvailable = 14, GCondNotScAvailable = 15,
            GCondMbAvailable = 16, GCondReadyingWs = 17, GCondReadyingMs = 18, GCondReadyingJa = 19, GCondCastingMa = 20,
            GCondCastingDebuff = 21, GCondRandom = 22, GCondNoSamba = 23, GCondNoStorm = 24, GCondPtHasTank = 25,
            GCondNotPtHasTank = 26, GCondIsEcosystem = 27, GCondHpMissing = 28, GCondCastingElementMa = 29,
            GCondCastEleMaSelf = 30, GCondCastingEleMaAoe = 31, GCondNeedEleBareffect = 32, GCondNoMaxRune = 33,
            GCondHasRunes = 34, GCondLungeMbAvailable = 35, GCondSubAnimation = 36, GCondJaOnCooldown = 37,
            GCondValUrielCheck = 38, GCondTimer = 39;
        // G_LOGIC, G_REACTION, G_SELECT
        private const ushort GLogicAnd = 0, GLogicOr = 1;
        private const ushort GReactionAttack = 0, GReactionRattack = 1, GReactionMa = 2, GReactionJa = 3, GReactionWs = 4,
            GReactionMs = 5, GReactionAnimString = 6;
        private const ushort GSelectHighest = 0, GSelectLowest = 1, GSelectSpecific = 2, GSelectRandom = 3, GSelectMbElement = 4,
            GSelectBestAgainstTarget = 7, GSelectBestSamba = 8, GSelectHighestWaltz = 9, GSelectEntrusted = 10, GSelectBestIndi = 11,
            GSelectStormDay = 12, GSelectHelixDay = 13, GSelectEnMobWeakness = 14, GSelectStormMobWeakness = 15,
            GSelectHelixMobWeakness = 16, GSelectDefBarElement = 17, GSelectRuneDay = 18, GSelectRandomAnimation = 19;

        // SPELLFAMILY by element fire .. dark (the nukes, the en-spells fire .. water, the storms, the helixes)
        private static readonly ushort[] NukeFamilies = { 42, 43, 44, 45, 46, 47, 7, 67 };
        private static readonly ushort[] EnFamilies = { 21, 22, 23, 24, 25, 26 };
        private static readonly ushort[] StormFamilies = { 33, 34, 32, 30, 35, 31, 37, 36 };
        private static readonly ushort[] HelixFamilies = { 79, 80, 78, 76, 81, 77, 83, 82 };
        // SpellID
        private const ushort SpellIndiRegen = 768, SpellIndiRefresh = 770, SpellIndiHaste = 771, SpellIndiFury = 779,
            SpellIndiAcumen = 781, SpellIndiPrecision = 783, SpellIndiFocus = 785, SpellIndiFrailty = 788;

        // ABILITY ids the JA selectors pick from
        private static readonly ushort[] GambitWaltzes = { 311, 193, 192, 191, 190 }; // Curing Waltz V .. I
        private const ushort AbilityDrainSamba = 184, AbilityDrainSambaII = 185, AbilityDrainSambaIII = 186, AbilityHasteSamba = 189;
        private const ushort AbilityIgnis = 358, AbilityGelus = 359, AbilityFlabra = 360, AbilityTellus = 361, AbilitySulpor = 362,
            AbilityUnda = 363, AbilityLux = 364, AbilityTenebrae = 365;

        private const ushort EffectDrainSamba = 368, EffectHasteSamba = 370;
        private static readonly ushort[] StormEffects =
            { 178, 179, 180, 181, 182, 183, 184, 185, 589, 590, 591, 592, 593, 594, 595, 596 };
        // Barfire .. Barwater (effects 100-105, spells 60-65), by element fire 1 .. water 6
        private const ushort EffectBarfire = 100, SpellBarfire = 60;
        private const ushort EffectGelus = 524, EffectFlabra = 525, EffectTellus = 526, EffectSulpor = 527, EffectUnda = 528,
            EffectLux = 529;
        private const string GambitCastElementVar = "[Gambit]CastElement", GambitLastUrielVar = "[Gambit]LastUrielTime";

        private static readonly byte[] GambitMeleeJobs = { 1, 2, 6, 7, 8, 9, 12, 13, 14, 16, 18, 19, 22 };
        private static readonly byte[] GambitCasterJobs = { 3, 4, 5, 10, 15, 16, 20, 21, 22 };
        private static readonly string[] GambitAnimations = { "sp00", "sp10", "sp20", "sp30", "sp40" };

        // CCharEntity::ForPartyWithTrusts: the members, then each member's trusts; alone, the char and its own
        private static List<BattleEntity> PartyWithTrusts(CharData c)
        {
            var list = new List<BattleEntity>();
            var members = c.Party?.Members.ToArray() ?? new[] { c };
            list.AddRange(members);
            foreach (var m in members) list.AddRange(m.PTrusts);
            return list;
        }

        private static bool GambitIsAlive(BattleEntity e) => e switch
        {
            MobEntity m => m.IsAlive && !m.IsDead,
            _ => e.Hp > 0,
        };

        // CTrustController::GetTopEnmity: the top of the master's battle target's enmity
        private BattleEntity TrustTopEnmity(TrustEntity trust) =>
            (trust.PMaster as CharData)?.BattleTarget is MobEntity mob ? mob.Enmity.GetHighestEnmity(_ => true, EnmityCharById) : null;

        // CGambitsContainer::Tick
        private void GambitsTick(TrustEntity trust, CharData master, long now)
        {
            long positionOffset = TrustPartyPosition(master, trust) * 100;
            if (now + positionOffset < trust.LastGambitActionMs) return;
            // Not already doing something
            if (trust.MagicState != null || trust.SkillState != null || trust.RangedState != null || trust.WsState != null ||
                trust.AbilityState != null) return;
            trust.LastGambitActionMs = now + MobRng.Next(2000, 3000);

            // Deal with TP skills before any gambits
            if (trust.Tp >= 1000 && TryTrustSkill(trust, now)) return;

            foreach (var gambit in trust.Gambits.ToArray())
            {
                if (now < gambit.LastUsedMs + gambit.RetryDelaySec * 1000L) continue;

                bool IsValidMember(BattleEntity member) =>
                    GambitIsAlive(member) && SameArea(member, trust) && Distance(trust, member) <= 15.0f;

                var potentialTargets = new List<BattleEntity>();
                switch (gambit.Target)
                {
                    case GTargetSelf:
                    case GTargetTriggerSelfActionTarget:
                        potentialTargets.Add(trust);
                        break;
                    case GTargetTarget:
                    case GTargetTriggerTargetActionSelf:
                        potentialTargets.Add(trust.BattleTarget);
                        break;
                    case GTargetParty:
                        potentialTargets.AddRange(PartyWithTrusts(master).Where(IsValidMember));
                        break;
                    case GTargetMaster:
                        potentialTargets.Add(trust.PMaster);
                        break;
                    case GTargetPartyDead:
                        if (trust.BattleTarget != null)
                            foreach (var m in master.Party?.Members.ToArray() ?? new[] { master })
                                if (m.Hp <= 0) potentialTargets.Add(m);
                        break;
                    case GTargetTank:
                        potentialTargets.AddRange(PartyWithTrusts(master).Where(m => IsValidMember(m) && m.MJob is MobStats.JobPld or MobStats.JobRun));
                        break;
                    case GTargetMelee:
                        potentialTargets.AddRange(PartyWithTrusts(master).Where(m => IsValidMember(m) && GambitMeleeJobs.Contains(m.MJob)));
                        break;
                    case GTargetRanged:
                        potentialTargets.AddRange(PartyWithTrusts(master).Where(m => IsValidMember(m) && m.MJob is MobStats.JobRng or 17));
                        break;
                    case GTargetCaster:
                        potentialTargets.AddRange(PartyWithTrusts(master).Where(m => IsValidMember(m) && GambitCasterJobs.Contains(m.MJob)));
                        break;
                    case GTargetTopEnmity:
                        if (trust.BattleTarget is MobEntity topMob)
                        {
                            var top = topMob.Enmity.GetHighestEnmity(_ => true, EnmityCharById);
                            potentialTargets.AddRange(PartyWithTrusts(master).Where(m => IsValidMember(m) && top == m));
                        }
                        break;
                    case GTargetCurilla:
                        potentialTargets.AddRange(PartyWithTrusts(master).Where(m =>
                            IsValidMember(m) && string.Equals(m.Name, "curilla", StringComparison.OrdinalIgnoreCase)));
                        break;
                }

                // For each potential target, check if the predicates resolves
                BattleEntity target = null;
                foreach (var potentialTarget in potentialTargets)
                {
                    bool all = true;
                    for (int g = 0; g < gambit.PredicateGroups.Count; g++)
                        if (!CheckGambitTrigger(trust, potentialTarget, gambit, g, now)) all = false;
                    if (!all) continue;
                    target = gambit.Target switch
                    {
                        // switch back to target before action for correct target selection in actions
                        GTargetTriggerSelfActionTarget => trust.BattleTarget,
                        GTargetTriggerTargetActionSelf => trust,
                        _ => potentialTarget,
                    };
                    break;
                }
                if (target == null) continue;

                // Pre resolve the actions: the first spell only (two spells in a tick do not work)
                var canExecute = new bool[gambit.Actions.Count];
                var resolvedSpells = new ushort?[gambit.Actions.Count];
                int firstMaIndex = -1;
                for (int i = 0; i < gambit.Actions.Count; i++)
                {
                    canExecute[i] = true;
                    var (reaction, select, argument) = gambit.Actions[i];
                    if (reaction == GReactionMa)
                    {
                        if (firstMaIndex == -1)
                        {
                            firstMaIndex = i;
                            resolvedSpells[i] = ResolveGambitSpell(trust, select, argument, target);
                            if (resolvedSpells[i] == null) canExecute[i] = false;
                        }
                        else canExecute[i] = false;
                    }
                    else if (reaction == GReactionJa && select == GSelectSpecific && loader_.Abilities?.Get((ushort)argument) == null)
                    {
                        canExecute[i] = false;
                    }
                }
                if (!canExecute.Any(v => v)) continue;

                bool executedAnyAction = false;
                for (int i = 0; i < gambit.Actions.Count; i++)
                {
                    if (!canExecute[i]) continue;
                    var (reaction, select, argument) = gambit.Actions[i];
                    switch (reaction)
                    {
                        case GReactionRattack:
                            TrustRangedAttack(trust, target, now);
                            executedAnyAction = true;
                            break;
                        case GReactionJa:
                        {
                            var ability = loader_.Abilities?.Get((ushort)argument);
                            // If SPECIFIC was validated earlier this shouldn't happen; skip this action.
                            if (ability == null) continue;
                            int mLevel = trust.MLvl;
                            if (select == GSelectHighestWaltz)
                            {
                                foreach (var waltzId in GambitWaltzes)
                                {
                                    var waltz = loader_.Abilities.Get(waltzId);
                                    if (waltz == null || mLevel < waltz.Level) continue;
                                    int tpCost = waltzId switch { 311 => 800, 193 => 650, 192 => 500, 191 => 350, 190 => 200, _ => 0 };
                                    if (tpCost != 0 && trust.Tp >= tpCost)
                                    {
                                        ability = waltz;
                                        MobAbility(trust, target, ability.Id, now);
                                        executedAnyAction = true;
                                    }
                                }
                            }

                            target = ability.ValidTarget == TargetSelf ? trust : trust.BattleTarget;

                            if (select == GSelectSpecific && target != null)
                            {
                                MobAbility(trust, target, ability.Id, now);
                                executedAnyAction = true;
                            }

                            if (select == GSelectBestSamba)
                            {
                                int tpCost = 0;
                                if (mLevel >= 5)
                                {
                                    ushort sambaId = 0;
                                    if (mLevel > 65) (sambaId, tpCost) = GambitPartyHasHealer(trust) ? (AbilityHasteSamba, 350) : (AbilityDrainSambaIII, 400);
                                    else if (mLevel < 65 && mLevel > 45)
                                        (sambaId, tpCost) = GambitPartyHasHealer(trust) ? (AbilityHasteSamba, 350) : (AbilityDrainSambaII, 250);
                                    else if (mLevel < 45 && mLevel > 35) (sambaId, tpCost) = (AbilityDrainSambaII, 250);
                                    else (sambaId, tpCost) = (AbilityDrainSamba, 100);
                                    if (loader_.Abilities.Get(sambaId) is { } samba) ability = samba;
                                    else tpCost = 0;
                                }
                                if (tpCost != 0 && trust.Tp >= tpCost)
                                {
                                    MobAbility(trust, target, ability.Id, now);
                                    executedAnyAction = true;
                                }
                            }
                            else if (select == GSelectRuneDay)
                            {
                                uint element = trust.LocalVars.TryGetValue(GambitCastElementVar, out var castElement) && castElement > 0
                                    ? castElement : DayElement();
                                ushort rune = element switch
                                {
                                    1 => AbilityUnda,     // fire
                                    2 => AbilityIgnis,    // ice
                                    3 => AbilityGelus,    // wind
                                    4 => AbilityFlabra,   // earth
                                    5 => AbilityTellus,   // thunder
                                    6 => AbilitySulpor,   // water
                                    7 => AbilityTenebrae, // light
                                    8 => AbilityLux,      // dark
                                    _ => AbilityIgnis,
                                };
                                MobAbility(trust, target, rune, now);
                                executedAnyAction = true;
                            }
                            break;
                        }
                        case GReactionMa:
                            // (an unresolved spell never gets here: its fallback in the C++ is unreachable)
                            TrustCast(trust, target, resolvedSpells[i].Value, now);
                            executedAnyAction = true;
                            break;
                        case GReactionMs:
                            if (select == GSelectSpecific)
                            {
                                MobUseSkill(trust, target, (ushort)argument, null, now);
                                executedAnyAction = true;
                            }
                            break;
                        case GReactionAnimString:
                            if (trust.PMaster == null || (trust.PathFind?.IsFollowingPath ?? false)) continue;
                            if (select == GSelectRandomAnimation)
                            {
                                int usableCount = Math.Clamp((int)argument, 1, GambitAnimations.Length);
                                PushMobInRange(trust, GambitSchedulor(trust, target, GambitAnimations[MobRng.Next(usableCount)]));
                                executedAnyAction = true;
                            }
                            else if (select == GSelectSpecific)
                            {
                                int animIndex = (int)argument;
                                if (animIndex >= GambitAnimations.Length) animIndex = MobRng.Next(3);
                                PushMobInRange(trust, GambitSchedulor(trust, target, GambitAnimations[animIndex]));
                                executedAnyAction = true;
                            }
                            break;
                    }
                }
                if (executedAnyAction && gambit.RetryDelaySec != 0) gambit.LastUsedMs = now;
            }
        }

        // GP_SERV_COMMAND_SCHEDULOR (0x038): the trust's animation string toward the target
        private static BasicPacket GambitSchedulor(TrustEntity trust, BattleEntity target, string anim)
        {
            var w = new PacketWriter(0x038, 20);
            w.U32(4, trust.Id);
            w.U32(8, target.Id);
            var bytes = System.Text.Encoding.ASCII.GetBytes(anim);
            for (int i = 0; i < 4 && i < bytes.Length; i++) w.U8(12 + i, bytes[i]);
            w.U16(16, trust.TargId);
            w.U16(18, target.TargId);
            return w.Packet;
        }

        // the ResolveMA lambda: the spell an MA action would cast now, if any
        private ushort? ResolveGambitSpell(TrustEntity trust, ushort select, uint argument, BattleEntity target)
        {
            switch (select)
            {
                case GSelectSpecific:
                    return GetAvailableSpell(trust, (ushort)argument);
                case GSelectHighest:
                    return GetBestAvailableSpell(trust, (ushort)argument);
                case GSelectDefBarElement:
                {
                    ushort? spellId = GetAvailableSpell(trust, SpellBarfire);
                    uint element = trust.LocalVars.TryGetValue(GambitCastElementVar, out var v) ? v : 0;
                    if (element is >= ElementFire and <= ElementWater) spellId = (ushort)(SpellBarfire + element - 1);
                    return spellId;
                }
                case GSelectRandom:
                    return GetSpellToCast(trust);
                case GSelectMbElement:
                {
                    var sc = target.StatusEffects.GetStatusEffect(EffectSkillchain, 0);
                    if (sc == null) return null;
                    var resonance = new List<byte>();
                    if (sc.Power != 0)
                    {
                        resonance.Add((byte)(sc.Power & 0xF));
                        resonance.Add((byte)(sc.Power >> 4 & 0xF));
                        resonance.Add((byte)(sc.Power >> 8));
                    }
                    ushort? spellId = null;
                    var damageList = trust.SpellContainer.DamageList;
                    foreach (var resonanceElement in resonance)
                    {
                        foreach (var chainElement in GetSkillchainMagicElement(resonanceElement))
                        {
                            // Iterating this list in reverse guarantees finding the best match
                            for (int ii = damageList.Count; ii > 0; --ii)
                            {
                                var spell = damageList[ii - 1];
                                if (GetSpell(spell)?.Element == chainElement)
                                {
                                    spellId = spell;
                                    break;
                                }
                            }
                        }
                    }
                    return spellId;
                }
                case GSelectBestIndi:
                    return trust.PMaster is CharData indiMaster ? GetBestIndiSpell(indiMaster) : null;
                case GSelectEntrusted:
                    // Entrusted spells target master
                    return trust.PMaster is CharData entrustMaster ? GetBestEntrustedSpell(entrustMaster) : null;
                case GSelectBestAgainstTarget:
                {
                    int weakness = WeakestElement(target);
                    if (GetSpell((ushort)argument) is { } spell && spell.Element == weakness) return (ushort)argument;
                    ushort? choice = weakness is >= 1 and <= 8 ? GetBestAvailableSpell(trust, NukeFamilies[weakness - 1]) : null;
                    // If all else fails, just cast the best you have!
                    return choice ?? GetBestAvailableSpell(trust, 0);
                }
                case GSelectStormDay:
                    return FamilyByElement(trust, StormFamilies, DayElement());
                case GSelectHelixDay:
                    return FamilyByElement(trust, HelixFamilies, DayElement());
                case GSelectEnMobWeakness:
                    return trust.BattleTarget == null ? null : FamilyByElement(trust, EnFamilies, (uint)WeakestElement(trust.BattleTarget));
                case GSelectStormMobWeakness:
                case GSelectHelixMobWeakness:
                    // (the helix selector asks for the storm too, as the C++ does)
                    return FamilyByElement(trust, StormFamilies, (uint)WeakestElement(target));
                default:
                    // LOWEST is not implemented in the C++ either
                    return null;
            }
        }

        // the element the target resists least: the first lowest of the eight resistance ranks, fire 1 .. dark 8
        private static int WeakestElement(BattleEntity target)
        {
            int weakestIndex = 0;
            for (int i = 1; i < 8; i++)
                if (target.GetMod((Mod)((int)Mod.FIRE_RES_RANK + i)) < target.GetMod((Mod)((int)Mod.FIRE_RES_RANK + weakestIndex))) weakestIndex = i;
            return weakestIndex + 1;
        }

        // the best available spell of the element's family in the table (none past its end)
        private ushort? FamilyByElement(MobEntity m, ushort[] families, uint element) =>
            element >= 1 && element <= families.Length ? GetBestAvailableSpell(m, families[element - 1]) : null;

        // CMobSpellContainer::GetBestIndiSpell: by the master's job (accuracy or magic accuracy short of the target
        // picks Precision / Focus), the sub choice below level 93, Regen below 30 for Refresh, nothing below 20
        private ushort? GetBestIndiSpell(CharData master)
        {
            var mTarget = master.BattleTarget;
            if (mTarget == null) return null;
            int hitrate = MeleeFor().HitRate(master, mTarget, 0);
            bool accBuffNeeded = hitrate < 65;
            int mInt = master.GetMod(Mod.INT), tInt = mTarget.GetMod(Mod.INT);
            int intDiff = mInt - tInt + 10;
            int macc = master.GetMod(Mod.MACC), tMaeva = mTarget.GetMod(Mod.MEVA);
            int mSkill = master.WorkingSkills[Spell.SkillElemental] & 0x7FFF;
            int maccFromInt = mInt;
            if (mInt > tInt + 10) maccFromInt = (int)(tInt + (mInt - intDiff) * 0.5);
            int totalMacc = mSkill + maccFromInt + macc;
            float magicHitRate = (float)(totalMacc - tMaeva) / 10;
            bool mAccBuffNeeded = magicHitRate < 10;

            ushort? choice = null;
            ushort subChoice = SpellIndiRegen;
            switch (master.MJob)
            {
                case 1: case 2: case 6: case 8: case 9: case 11: case 12: case 14: case 16: case 17: case 18: case 19:
                    choice = accBuffNeeded ? SpellIndiPrecision : SpellIndiFury;
                    subChoice = SpellIndiRegen;
                    break;
                case 3: case 10: case 15: case 21:
                    choice = SpellIndiRefresh;
                    subChoice = SpellIndiRefresh;
                    break;
                case 4: case 5: case 20:
                    choice = mAccBuffNeeded ? SpellIndiFocus : SpellIndiAcumen;
                    subChoice = SpellIndiRefresh;
                    break;
                case 7: case 22: case 13:
                    choice = SpellIndiHaste;
                    subChoice = SpellIndiRegen;
                    break;
            }
            if (master.MLvl < 20) choice = null;
            else if (master.MLvl < 93)
            {
                choice = subChoice;
                if (subChoice == SpellIndiRefresh && master.MLvl < 30) choice = SpellIndiRegen;
            }
            return choice;
        }

        // CMobSpellContainer::GetBestEntrustedSpell: by the master's job
        private static ushort? GetBestEntrustedSpell(CharData master) => master.MJob switch
        {
            1 or 2 or 6 or 8 or 9 or 11 or 12 or 14 or 16 or 17 or 18 or 19 => SpellIndiFrailty,
            3 or 10 or 15 => SpellIndiAcumen,
            4 or 5 or 20 or 7 or 22 => SpellIndiRefresh,
            13 => SpellIndiRegen,
            _ => null,
        };

        // CMobSpellContainer::GetBestAvailable: the family's last affordable spell off recast (the damage list alone for
        // SPELLFAMILY_NONE); no Manafont here
        private ushort? GetBestAvailableSpell(MobEntity m, ushort family)
        {
            var c = m.SpellContainer;
            var lists = family == 0
                ? new[] { c.DamageList }
                : new[] { c.GaList, c.DamageList, c.BuffList, c.DebuffList, c.HealList, c.NaList, c.RaiseList };
            ushort? best = null;
            foreach (var list in lists)
            {
                foreach (var id in list)
                {
                    var spell = SpellRow(id);
                    if (spell == null) continue;
                    bool sameFamily = family == 0 || spell.Family == family;
                    bool free = spell.Skill is Spell.SkillNinjutsu or Spell.SkillSinging or Spell.SkillWind or Spell.SkillString or Spell.SkillGeomancy;
                    bool enoughMp = spell.MpCost <= m.Mp || free;
                    if (sameFamily && enoughMp && !RecastsOf(m).Has(RecastType.Magic, id)) best = id;
                }
            }
            return best;
        }

        // CTrustController::Cast: off recast, a self-only spell on itself; held back while another of the party's trusts
        // casts the same kind (a buff or debuff of its family as good or better, a cure on the same target above 50%,
        // the same -na), then CMobController::Cast
        private bool TrustCast(TrustEntity trust, BattleEntity target, ushort spellId, long now)
        {
            FaceTarget(trust, target);
            if (RecastsOf(trust).Has(RecastType.Magic, spellId)) return false;
            var spell = GetSpell(spellId);
            if (spell == null) return false;
            var castTarget = spell.ValidTargets == TargetSelf ? trust : target;
            if (trust.PMaster is CharData master)
            {
                foreach (var member in PartyWithTrusts(master))
                {
                    if (member is not TrustEntity other || other.MagicState is not { } state) continue;
                    var mSpell = state.Spell;
                    if (spell.IsBuff && spell.Family == mSpell.Family && spellId <= mSpell.Id) return false;
                    if (spell.IsCure && castTarget == state.Target && castTarget.Hpp > 50) return false;
                    if (spell.IsDebuff && spell.Family == mSpell.Family && spellId <= mSpell.Id) return false;
                    if (spell.IsNa && spell.Family == mSpell.Family && spellId == mSpell.Id) return false;
                }
            }
            return MobCast(trust, castTarget, spellId, now);
        }

        private static bool GambitPartyHasJob(TrustEntity trust, params byte[] jobs) =>
            trust.PMaster is CharData master && PartyWithTrusts(master).Any(m => jobs.Contains(m.MJob));

        // CGambitsContainer::PartyHasHealer / PartyHasTank
        private static bool GambitPartyHasHealer(TrustEntity trust) =>
            GambitPartyHasJob(trust, MobStats.JobWhm, MobStats.JobRdm, MobStats.JobPld, MobStats.JobSch);
        private static bool GambitPartyHasTank(TrustEntity trust) => GambitPartyHasJob(trust, MobStats.JobNin, MobStats.JobPld, MobStats.JobRun);

        // what the entity casts now, if anything
        private static MagicCastState CastingState(BattleEntity e) => e.MagicState;

        // CGambitsContainer::CheckTrigger: each predicate of the group, then AND (all, so true when empty) or OR
        private bool CheckGambitTrigger(TrustEntity trust, BattleEntity triggerTarget, TrustGambit gambit, int groupIndex, long now)
        {
            if (triggerTarget == null) return false;
            var (logic, predicates) = gambit.PredicateGroups[groupIndex];
            var results = new List<bool>();
            for (int predicateIndex = 0; predicateIndex < predicates.Count; predicateIndex++)
            {
                var (condition, arg) = predicates[predicateIndex];
                var fx = triggerTarget.StatusEffects;
                bool result;
                switch (condition)
                {
                    case GCondAlways: result = true; break;
                    case GCondHppLt: result = triggerTarget.Hpp < arg; break;
                    case GCondHppGte: result = triggerTarget.Hpp >= arg; break;
                    case GCondMppLt: result = triggerTarget.Mpp < arg; break;
                    case GCondMppGte: result = triggerTarget.Mpp >= arg; break;
                    case GCondTpLt: result = triggerTarget.Tp < (short)arg; break;
                    case GCondTpGte: result = triggerTarget.Tp >= (short)arg; break;
                    case GCondStatus: result = fx.HasStatusEffect((ushort)arg); break;
                    case GCondNotStatus: result = !fx.HasStatusEffect((ushort)arg); break;
                    case GCondTimer:
                    {
                        if (arg == 0)
                        {
                            result = true;
                            break;
                        }
                        string key = $"{gambit.Id}:{groupIndex}:{predicateIndex}";
                        if (!trust.TimerConditionLastTrigger.TryGetValue(key, out long last))
                        {
                            // first evaluation is true, then gated by interval
                            trust.TimerConditionLastTrigger[key] = now;
                            result = true;
                        }
                        else if (now - last >= arg * 1000L)
                        {
                            trust.TimerConditionLastTrigger[key] = now;
                            result = true;
                        }
                        else result = false;
                        break;
                    }
                    case GCondJaOnCooldown:
                        result = (loader_.Abilities?.Get((ushort)arg)?.RecastTimeMs ?? 0) > 0;
                        break;
                    case GCondLvlLt: result = triggerTarget.MLvl < arg; break;
                    case GCondLvlGte: result = triggerTarget.MLvl >= arg; break;
                    case GCondHasRunes: result = Runes(triggerTarget).Any(); break;
                    case GCondNoMaxRune:
                    {
                        int maxRuneEffect = 1;
                        if (trust.MJob == MobStats.JobRun)
                        {
                            if (trust.MLvl >= 65) maxRuneEffect = 3;
                            else if (trust.MLvl >= 35) maxRuneEffect = 2;
                        }
                        result = Runes(triggerTarget).Count() < maxRuneEffect;
                        break;
                    }
                    case GCondNoSamba: result = !fx.HasStatusEffect(EffectDrainSamba) && !fx.HasStatusEffect(EffectHasteSamba); break;
                    case GCondNoStorm: result = !StormEffects.Any(id => fx.HasStatusEffect(id)); break;
                    case GCondPtHasTank: result = GambitPartyHasTank(trust); break;
                    case GCondNotPtHasTank: result = !GambitPartyHasTank(trust); break;
                    case GCondStatusFlag: result = fx.HasStatusEffectByFlag(arg); break;
                    case GCondHasTopEnmity:
                    {
                        var top = TrustTopEnmity(trust);
                        result = top != null && top.TargId == trust.TargId;
                        break;
                    }
                    case GCondNotHasTopEnmity:
                    {
                        var top = TrustTopEnmity(trust);
                        result = top != null && top.TargId != trust.TargId;
                        break;
                    }
                    case GCondScAvailable:
                    {
                        var sc = fx.GetStatusEffect(EffectSkillchain);
                        result = sc != null && sc.StartMs + 3000 < now && sc.Tier == 0;
                        break;
                    }
                    case GCondNotScAvailable: result = fx.GetStatusEffect(EffectSkillchain) == null; break;
                    case GCondMbAvailable:
                    {
                        var sc = fx.GetStatusEffect(EffectSkillchain);
                        result = sc != null && sc.StartMs + 3000 < now && sc.Tier > 0;
                        break;
                    }
                    case GCondLungeMbAvailable:
                    {
                        var sc = fx.GetStatusEffect(EffectSkillchain);
                        bool maybeUseLunge = false;
                        if (sc != null && sc.StartMs + 3000 < now && sc.Tier > 0)
                        {
                            var element = (byte)sc.Power;
                            if (element != 0 && GetSkillchainTier(element) >= 3)
                            {
                                var own = trust.StatusEffects;
                                if (element is ScLight or ScLightII)
                                    maybeUseLunge = own.HasStatusEffect(EffectLux) || own.HasStatusEffect(EffectIgnis) ||
                                                    own.HasStatusEffect(EffectFlabra) || own.HasStatusEffect(EffectSulpor);
                                else if (element is ScDarkness or ScDarknessII)
                                    maybeUseLunge = own.HasStatusEffect(EffectTenebrae) || own.HasStatusEffect(EffectTellus) ||
                                                    own.HasStatusEffect(EffectUnda) || own.HasStatusEffect(EffectGelus);
                            }
                        }
                        result = maybeUseLunge;
                        break;
                    }
                    case GCondReadyingWs: result = triggerTarget is CharData { WsState: not null } or TrustEntity { WsState: not null }; break;
                    case GCondReadyingMs: result = triggerTarget is MobEntity { SkillState: not null }; break;
                    case GCondReadyingJa: result = triggerTarget is CharData { AbilityState: not null } or TrustEntity { AbilityState: not null }; break;
                    case GCondCastingMa: result = CastingState(triggerTarget) != null; break;
                    case GCondCastingDebuff: result = CastingState(triggerTarget)?.Spell.IsDebuff ?? false; break;
                    case GCondCastingEleMaAoe:
                    {
                        var spell = CastingState(triggerTarget)?.Spell;
                        result = spell != null && spell.Element is >= ElementFire and <= ElementWater && spell.Aoe == Spell.AoeRadial;
                        break;
                    }
                    case GCondCastingElementMa:
                    {
                        var spell = CastingState(triggerTarget)?.Spell;
                        result = spell != null && spell.Element is >= ElementFire and <= ElementWater;
                        break;
                    }
                    case GCondCastEleMaSelf:
                    {
                        var state = CastingState(triggerTarget);
                        result = state != null && state.Target?.Id == trust.Id && state.Spell.Element is >= ElementFire and <= ElementWater;
                        break;
                    }
                    case GCondNeedEleBareffect:
                    {
                        bool needBarEffect = false;
                        if (CastingState(triggerTarget) is { } state)
                        {
                            uint element = state.Spell.Element;
                            if (element is >= ElementFire and <= ElementWater)
                                needBarEffect = !trust.StatusEffects.HasStatusEffect((ushort)(EffectBarfire + element - 1));
                            else element = DayElement();
                            trust.LocalVars[GambitCastElementVar] = element;
                        }
                        result = needBarEffect;
                        break;
                    }
                    case GCondIsEcosystem:
                        result = (triggerTarget is MobEntity em ? em.Ecosystem : triggerTarget is CharData tc ? tc.EcoSystem : 0) == arg;
                        break;
                    case GCondRandom: result = MobRng.Next(100) < (short)arg; break;
                    case GCondHpMissing: result = triggerTarget.MaxHp - triggerTarget.Hp >= (short)arg; break;
                    case GCondSubAnimation:
                        result = (triggerTarget switch { MobEntity sm => sm.AnimationSub, CharData sc => sc.AnimationSub, _ => 0 }) == arg;
                        break;
                    case GCondValUrielCheck: result = ValUrielCheck(trust); break;
                    default: result = false; break;
                }
                results.Add(result);
            }
            return logic switch
            {
                GLogicAnd => results.All(r => r),
                GLogicOr => results.Any(r => r),
                _ => false,
            };
        }

        // G_CONDITION::VAL_URIEL_CHECK: the master has hate the trust does not top, or a mob within 10 yalms of the trust
        // is on the master other than its target (the master's notoriety container); then the cooldowns (30 s,
        // 5 s for a new target) since [Gambit]LastUrielTime, and the target within 10 yalms
        private bool ValUrielCheck(TrustEntity trust)
        {
            if (trust.PMaster is not CharData master || master.BattleTarget is not MobEntity mob) return false;
            bool masterHasEnmity = mob.Enmity.HasId(master.Id);
            bool valHasEnmity = mob.Enmity.HasId(trust.Id);
            var top = TrustTopEnmity(trust);
            bool valHasTopEnmity = top != null && top.TargId == trust.TargId;
            bool masterHasOffTargetAggro = false;
            if (master.Notoriety.HasEnmity())
                foreach (var zoneMob in master.Notoriety)
                {
                    if (!zoneMob.IsAlive || zoneMob.IsDead) continue;
                    if (Distance(trust, zoneMob) > 10.0f) continue;
                    if (zoneMob.BattleTarget == master && zoneMob.Id != mob.Id && zoneMob.Allegiance != master.Allegiance)
                    {
                        masterHasOffTargetAggro = true;
                        break;
                    }
                }
            if (!(masterHasEnmity && !valHasTopEnmity) && !masterHasOffTargetAggro) return false;
            long timeNow = UnixNow();
            uint lastUrielTime = trust.LocalVars.TryGetValue(GambitLastUrielVar, out var t) ? t : 0;
            long timeDiff = timeNow - lastUrielTime;
            bool longCooldown = timeDiff >= 30, shortCooldown = timeDiff >= 5;
            return Distance(trust, mob) <= 10.0f &&
                   (valHasEnmity && longCooldown || !valHasEnmity && shortCooldown || masterHasOffTargetAggro && longCooldown);
        }
    }
}
