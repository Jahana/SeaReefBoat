using System;
using System.Collections.Generic;
using XI.Map;
using XI.Script;

namespace XI.Zone
{
    // CMagicState for any caster, a mob or a char: init (the spell, IsValidTarget, CanCastSpell, onMagicCastingCheck,
    // the cast time, the "starts casting" 0x028), Update (the target lost or a prevent-action effect mid cast; at the
    // end the target, cost and range again, a char's movement (HasMoved), a locked target, paralysis and
    // intimidation, the hit or effect interrupt, the finish; the animation time before the state ends), Cleanup, the
    // cost (MP, a char's ninja tools), the recast and ApplyEnmity; CBattleEntity::OnCastFinished / OnCastInterrupted
    // with the mob's and the char's own halves (CMobEntity: onSpellPrecast / onSpellInterrupted, the controller's
    // OnCastStopped, TapDeaggroTime; CCharEntity: the error message, the last-spell time canAct reads,
    // RemoveStratagems, the magic and instrument skill-ups, the RoE heal / buff ally events); battleutils'
    // CalculateSpellCastTime / Cost / RecastTime (with a char's merits and job points and the Alacrity / Celerity
    // weather bonus), CanAffordSpell, HasNinjaTool, TryInterruptSpell, IsParalyzed, IsIntimidated, GenerateInRangeEnmity
    // and DirtyExp; spell::CanUseSpell for a char (the learned spell, the main / sub job level, Tabula Rasa, the
    // addendums, a set blue spell or Unbridled Learning, xi.spells.canUseSpellOverride). The mob controller's casting
    // AI is ZoneHost.MobMagic.cs; the 0x01A cast is ZoneHost.PlayerMagic.cs. The bindings: castSpell, takeSpellDamage,
    // hasSpellList, getSpellListId, tryHitInterrupt. Pets and trusts cast through the same state; a locked char (or a
    // pet whose master is) stops the cast, as does a trust spell over the battlefield's trust count
    // (xi.trust.checkBattlefieldTrustCount); Convergence makes a blue AoE single target; a pet's or trust's spell
    // claims for its master (not an automaton's) and a trust's RoE credit goes to its master. takeSpellDamage runs
    // Scarlet Delirium and BindBreakCheck. A spell that kills a mob delays its defeat message by the spell's animation
    // time (m_DropItemTime). Not ported: the empty action C++ pushes when the target goes invalid mid cast (it carries
    // no target).
    public sealed class MagicCastState
    {
        public Spell Spell;        // the state's clone (the scripts may change it)
        public BattleEntity Target;
        public long EntryMs, CastMs;
        public byte Flags;         // MAGICFLAGS_*
        public bool Completed, Interrupted, InstantCast;
        public long SwingRemainingMs; // the attack state's countdown, held while this state runs
        public float StartX, StartY, StartZ; // m_startPos
        public bool StartedMoving;  // m_startedMoving: a char moving when the cast began
        public BasicPacket ErrorMsg; // m_errorMsg: what a char is told when the cast is refused or interrupted
    }

    public sealed partial class ZoneHost
    {
        private const byte MagicFlagIgnoreMp = 0x01, MagicFlagIgnoreTools = 0x02;
        private const ushort MsgStartsCastingSelf = 3, MsgIsInterrupted = 16, MsgUnableToCast = 18, MsgNotEnoughMp = 34,
            MsgNoNinjaTools = 35, MsgCannotCastSpell = 47, MsgUnableToCastSpells = 49,
            MsgCannotPerformAction = 71, MsgIsParalyzed2 = 84, MsgIsIntimidated = 106, MsgOutOfRangeUnableCast = 313,
            MsgStartsCastingTarget = 327, MsgMagicNoEffect = 75, MsgMagicTeleport = 93;
        private const ushort JobSch = 20;
        private const ushort AnimTeleport = 122; // ActionAnimation::Teleport
        private const float MagicMovementThreshold = 0.3f; // movementThreshold
        private const long CanActAfterSpellMs = 2500;     // CPlayerController::canAct

        // SPELLREQ_*
        private const byte SpellReqAddendumBlack = 0x02, SpellReqAddendumWhite = 0x04, SpellReqTabulaRasa = 0x08,
            SpellReqUnbridledLearning = 0x10, SpellReqMainJobOnly = 0x20;
        // SPELLFAMILY_*
        private const ushort FamilyUtsusemi = 96, FamilyIndiBuff = 144, FamilyIndiDebuff = 145;
        // xi::Merit, the job points (JPCATEGORY_* + n) and the trait the formulas read
        private const ushort MeritSpellInterruptionRate = 328, MeritCureCastTime = 514, MeritLullabyRecast = 960,
            MeritFinaleRecast = 962, MeritSummoningMagicCastTime = 1288, MeritNightingale = 2624, MeritNinjaToolExpertise = 2818;
        private const int JpQuickMagicEffect = 0x0A0 + 0x08, JpNinjutsuCastTimeBonus = 0x1A0 + 0x05,
            JpStratagemEffectII = 0x280 + 0x05, JpWidenedCompassEffect = 0x2A0 + 0x02;
        private const int TraitNinjaToolExpert = 87, TraitShieldMastery = 25;
        // ROE_EVENT
        private const int RoeEventHealAlly = 8, RoeEventBuffAlly = 9, RoeEventHealUnityAlly = 17;
        // the SpellIDs the recast merits name
        private const ushort SpellMagicFinale = 462, SpellFoeLullaby = 463, SpellHordeLullaby = 376, SpellFoeLullabyII = 471,
            SpellHordeLullabyII = 377;

        // the xi::StatusEffect ids casting reads
        private static class MagicFx
        {
            public const ushort Intimidate = 22, Mute = 29, Aquaveil = 39, Manafont = 47, Chainspell = 48, Spontaneity = 230,
                Marcato = 231, Omerta = 262, Nightingale = 347, Troubadour = 348, Hasso = 353, Seigan = 354, LightArts = 358,
                DarkArts = 359, Penury = 360, Parsimony = 361, Celerity = 362, Alacrity = 363, Ebullience = 365,
                Accession = 366, Manifestation = 367, TabulaRasa = 377, AddendumWhite = 401, AddendumBlack = 402,
                Pianissimo = 409, Altruism = 412, Focalization = 413, Tranquility = 414, Equanimity = 415, Enlightenment = 416,
                Composure = 419, Yonin = 420, DivineEmblem = 438, Futae = 441, Saboteur = 454, Perpetuance = 469,
                UnbridledLearning = 485, SubtleSorcery = 493, Stymie = 494, UnbridledWisdom = 505, Doubt = 576, Entrust = 584,
                Cascade = 598, Convergence = 355;
        }

        private const ushort MsgTrustNoCastTrust = 700; // You are unable to use Trust magic at this time.

        // ---- the state ----------------------------------------------------------------------------------------------

        // CMagicState::init: false refuses the cast; a char is sent why (HandleErrorMessage), a mob is not
        private bool StartMagic(BattleEntity caster, BattleEntity target, ushort id, long now, byte flags = 0)
        {
            var c = caster as CharData;
            if (caster is MobEntity m && m.GetMobMod(MobMod.NoSpellCost) > 0) flags |= MagicFlagIgnoreMp;
            var row = GetSpell(id);
            if (row == null)
            {
                if (c != null) PushTo(c, SelfPackets.BattleMessage(c, id, 0, MsgCannotCastSpell));
                return false;
            }
            var st = new MagicCastState { Spell = row.Clone(), Flags = flags, EntryMs = now };
            var valid = MagicValidTarget(caster, target, st.Spell.ValidTargets, out st.ErrorMsg);
            if (valid == null || st.ErrorMsg != null || !CanCastSpell(caster, st, valid, false))
            {
                if (c != null && st.ErrorMsg != null) PushTo(c, st.ErrorMsg);
                return false;
            }
            st.Target = valid;
            int check = OnMagicCastingCheck(caster, valid, st.Spell);
            if (check != 0)
            {
                if (c != null)
                    PushTo(c, SelfPackets.BattleMessage(c.Id, c.TargId, valid.Id, valid.TargId, st.Spell.Id, 0,
                        check == 1 ? MsgCannotCastSpell : (ushort)check));
                return false;
            }
            st.CastMs = CalculateSpellCastTime(caster, st);
            st.StartX = caster.X;
            st.StartY = caster.Y;
            st.StartZ = caster.Z;
            if (c != null) st.StartedMoving = c.LastMoveDistance > MagicMovementThreshold;
            st.SwingRemainingMs = Math.Max(0, SwingAtOf(caster) - now);
            caster.MagicState = st;

            uint shownId = c == null && ChatCfg.HideReadiesTarget ? caster.Id : valid.Id;
            var start = new ActionPacket { ActorId = caster.Id, ActionType = ActionCategory.MagicStart, ActionId = (uint)st.Spell.FourCCOf(false) };
            var r = start.AddTarget(shownId).AddResult();
            r.Param = st.Spell.Id;
            r.MessageId = c == null ? MsgStartsCastingSelf : MsgStartsCastingTarget;
            if (caster is MobEntity mob) CallMobHook(mob, "onSpellCastStart", mob, valid, st.Spell);
            TriggerListener(caster, "MAGIC_START", caster, valid, st.Spell, start);
            if ((st.Spell.Flag & Spell.FlagNoStartMsg) != 0) ZeroMessages(start);
            PushCasterInRange(caster, Battle2Packets.Battle2(start));
            lua_?.Forget(start);
            return true;
        }

        // IsValidTarget for the caster: a char's (CCharEntity's, with the message to refuse with) or a mob's
        private BattleEntity MagicValidTarget(BattleEntity caster, BattleEntity target, ushort flags, out BasicPacket err)
        {
            err = null;
            if (caster is CharData c)
            {
                if (target != null && !EntityExists(target)) target = null;
                return AbilityValidTarget(c, target, flags, out err);
            }
            return target != null && EntityExists(target) && ValidSkillTarget(target, (MobEntity)caster, flags) ? target : null;
        }

        private static long SwingAtOf(BattleEntity b) => b is CharData c ? c.SwingAtMs : ((MobEntity)b).SwingAtMs;

        // CHAR_INRANGE_SELF: a char's packets reach it and those near it, a mob's those that have it spawned
        private void PushCasterInRange(BattleEntity b, BasicPacket p)
        {
            if (b is CharData c) PushCharInRange(c, p, true);
            else PushMobInRange((MobEntity)b, p);
        }

        private static void ZeroMessages(ActionPacket action)
        {
            foreach (var t in action.Targets)
                foreach (var r in t.Results)
                    r.MessageId = 0;
        }

        // the state's turn each tick: done once the animation time after the cast has passed
        private void TickMagic(BattleEntity b, long now)
        {
            if (b.MagicState != null && UpdateMagic(b, now)) EndMagic(b, now);
        }

        // the chars' magic states (a mob's ticks with its AI)
        private void TickCharMagic()
        {
            long now = NowMs();
            foreach (var (c, _) in new List<KeyValuePair<CharData, MapSession>>(charSessions_))
                TickMagic(c, now);
        }

        // CMagicState::Update: true when the state is done. A cast stopped early is completed but stays on top until
        // its cast and animation time have passed, as in C++.
        private bool UpdateMagic(BattleEntity caster, long now)
        {
            var st = caster.MagicState;
            if (st == null) return false;
            var spell = st.Spell;
            var target = st.Target;
            // mobs only print the interrupted message when a hit or status interrupted them
            bool quiet = caster is not CharData;
            // a mob loses a target hidden from it (CheckHide) mid cast
            bool TargetValid() => MagicValidTarget(caster, target, spell.ValidTargets, out st.ErrorMsg) != null && st.ErrorMsg == null &&
                                  !(caster is MobEntity mc && target != null && target != caster && CheckHide(mc, target));

            if (!st.Completed)
            {
                if (!TargetValid())
                {
                    OnCastInterrupted(caster, st, MsgIsInterrupted, quiet, now);
                    st.Completed = true;
                    return false;
                }
                if (spell.Group != Spell.GroupSong && caster.StatusEffects.HasPreventActionEffect()) st.Interrupted = true;
            }

            if (now > st.EntryMs + st.CastMs && !st.Completed)
            {
                if (!TargetValid() || !CanCastSpell(caster, st, target, true) || HasMoved(caster, st))
                {
                    OnCastInterrupted(caster, st, MsgIsInterrupted, quiet, now);
                    st.Completed = true;
                    return false;
                }
                if (target is CharData tc)
                {
                    if (tc.Locked)
                    {
                        OnCastInterrupted(caster, st, MsgIsInterrupted, true, now);
                        st.Completed = true;
                        return false;
                    }
                    // the trust count would go over the battlefield's limit (simultaneous multi-party casts)
                    if (spell.Group == Spell.GroupTrust && !CallOptionalGlobalBool("xi.trust.checkBattlefieldTrustCount", tc))
                    {
                        OnCastInterrupted(caster, st, MsgTrustNoCastTrust, true, now);
                        st.Completed = true;
                        return false;
                    }
                }
                else if (target is PetEntity tp)
                {
                    // a pet with no char master leaves the cast hanging, as in C++
                    if (tp.PMaster is not CharData pc) return false;
                    if (pc.Locked)
                    {
                        OnCastInterrupted(caster, st, MsgIsInterrupted, true, now);
                        st.Completed = true;
                        return false;
                    }
                }
                // Super Jump or otherwise untargetable
                if (IsUntargetable(target))
                {
                    OnCastInterrupted(caster, st, MsgIsInterrupted, true, now);
                    st.Completed = true;
                    return false;
                }
                if (IsParalyzed(caster))
                {
                    MagicStopped(caster, spell, target, MsgIsParalyzed2, false);
                    if (caster is MobEntity pm) OnCastStopped(pm, now);
                    st.Completed = true;
                    return false;
                }
                if (IsIntimidated(caster, target))
                {
                    MagicStopped(caster, spell, target, MsgIsIntimidated, true);
                    if (caster is MobEntity im) OnCastStopped(im, now);
                    st.Completed = true;
                    return false;
                }
                if (caster.StatusEffects.HasPreventActionEffect()) st.Interrupted = true;

                var action = new ActionPacket();
                if (st.Interrupted)
                {
                    OnCastInterrupted(caster, st, MsgIsInterrupted, false, now);
                    TriggerListener(caster, "MAGIC_INTERRUPTED", caster, target, spell, action);
                }
                else
                {
                    OnCastFinished(caster, st, action, now);
                    TriggerListener(caster, "MAGIC_USE", caster, target, spell, action);
                    foreach (var t in action.Targets)
                        if (BattleById(caster.Zone, t.ActorId) is { } taker) TriggerListener(taker, "MAGIC_TAKE", taker, caster, spell, action);
                }
                if ((spell.Flag & Spell.FlagNoFinishMsg) != 0) ZeroMessages(action);
                if (action.Targets.Count != 0) PushCasterInRange(caster, Battle2Packets.Battle2(action));
                lua_?.Forget(action);
                if (caster is CharData uc && SessionOf(uc) is { } us) SendUpdateMask(us, uc);
                st.Completed = true;
            }
            else if (st.Completed && now > st.EntryMs + st.CastMs + spell.AnimationTimeMs)
            {
                if (caster is CharData hc) hc.History[HistorySpellsCast]++;
                TriggerListener(caster, "MAGIC_STATE_EXIT", caster, spell);
                return true;
            }
            return false;
        }

        // CMagicState::HasMoved: a char that was moving when it began, or that went more than 0.3 across the ground
        private static bool HasMoved(BattleEntity caster, MagicCastState st)
        {
            if (caster is not CharData) return false;
            if (st.StartedMoving) return true;
            float dx = caster.X - st.StartX, dz = caster.Z - st.StartZ;
            return MathF.Sqrt(dx * dx + dz * dz) > MagicMovementThreshold;
        }

        private BattleEntity BattleById(ushort zone, uint id)
        {
            if (CharById(id).Item1 is CharData c) return c;
            return MobById(id) is { } mob && mob.Zone == zone ? mob : null;
        }

        // the state finishing (resumeNextState): its cleanup, then the attack state picks its countdown up again
        private void EndMagic(BattleEntity b, long now)
        {
            var st = b.MagicState;
            if (st == null) return;
            // CMagicState::Cleanup: a cast that never finished is interrupted (quietly, a mob)
            if (!st.Completed) OnCastInterrupted(b, st, MsgIsInterrupted, b is not CharData, now);
            if (b.MagicState == st) b.MagicState = null;
            if (b is MobEntity m) m.SwingAtMs = now + st.SwingRemainingMs;
            else if (b is CharData c && c.AttackTarget != null) c.SwingAtMs = now + st.SwingRemainingMs;
            lua_?.Forget(st.Spell);
        }

        // ClearStateStack / a forced despawn or zone out: the magic state cleaned up where it stands
        private void AbortMagic(BattleEntity b, long now)
        {
            if (b.MagicState != null) EndMagic(b, now);
        }

        // CMagicState::CanCastSpell: the caster's CanUseSpell, the zone, silence, Omerta, the cost, then the target's
        // range (a char's from the spell's own, a mob's 28.5); a char's refusal is left in ErrorMsg
        private bool CanCastSpell(BattleEntity caster, MagicCastState st, BattleEntity target, bool isEndOfCast)
        {
            var spell = st.Spell;
            var c = caster as CharData;
            BasicPacket Msg(BattleEntity to, ushort msg) => SelfPackets.BattleMessage(caster.Id, caster.TargId, to.Id, to.TargId, spell.Id, 0, msg);
            if (!CanUseSpell(caster, spell))
            {
                st.ErrorMsg = Msg(caster, MsgCannotCastSpell);
                return false;
            }
            if (!AreaOf(caster).CanUseMisc(spell.ZoneMisc))
            {
                st.ErrorMsg = Msg(caster, MsgCannotInThisArea);
                return false;
            }
            if (caster.StatusEffects.HasStatusEffect(EffectId.Silence, MagicFx.Mute))
            {
                st.ErrorMsg = Msg(caster, MsgUnableToCastSpells);
                return false;
            }
            if (caster.StatusEffects.GetStatusEffect(MagicFx.Omerta) is { } omerta && spell.Group > 0 && ((1 << (spell.Group - 1)) & omerta.Power) != 0)
            {
                st.ErrorMsg = Msg(caster, MsgUnableToCastSpells);
                return false;
            }
            if (!HasMagicCost(caster, st)) return false;
            if (target == null)
            {
                st.ErrorMsg = Msg(caster, MsgBasicCannotOnThatTarget);
                return false;
            }
            // PTarget->IsNameHidden()
            if (target is MobEntity tm && (tm.NameVis & MobEntity.NameVisHideName) != 0) return false;
            if (target is CharData tc && (tc.NameVis & MobEntity.NameVisHideName) != 0) return false;
            if (target == caster) return true;
            float dist = Distance(caster, target);
            if (dist > 40)
            {
                st.ErrorMsg = Msg(target, MsgTooFarAway);
                return false;
            }
            if (c != null)
            {
                float spellRange = spell.Range + HitboxSize(target) + HitboxSize(caster);
                // Entrust has a 25 yalm range for Indi spells (not affected by hitboxes)
                if (c.StatusEffects.HasStatusEffect(MagicFx.Entrust) && spell.Family is FamilyIndiBuff or FamilyIndiDebuff) spellRange = 25.0f;
                if (dist > spellRange)
                {
                    st.ErrorMsg = Msg(target, MsgOutOfRangeUnableCast);
                    return false;
                }
            }
            if (caster is MobEntity && dist > 28.5f) return false;
            // a char in a zone that blocks it needs a line of sight while it casts
            if (!isEndOfCast && caster is CharData lc && ZoneInfoOf(lc.Zone).CanUseMisc("los_player_block") && !CanSeeTarget(caster, target))
            {
                st.ErrorMsg = Msg(target, MsgCannotPerformAction);
                return false;
            }
            return true;
        }

        // CBattleEntity::CanUseSpell (spell::CanUseSpell, then the recast) with CCharEntity's learned-spell check
        private bool CanUseSpell(BattleEntity caster, Spell spell)
        {
            if (caster is CharData c)
            {
                if (c.Monstrosity != null && !Monstrosity.CanCastSpell(spell)) return false;
                if (spell.Id >= c.SpellList.Length * 8 || (c.SpellList[spell.Id / 8] & (1 << (spell.Id % 8))) == 0) return false;
                if (!CharCanUseSpell(c, spell)) return false;
            }
            else if (caster is MobEntity m)
            {
                if ((m.NameVis & MobEntity.NameVisHideName) != 0 || m.Untargetable) return false;
            }
            return !RecastsOf(caster).Has(RecastType.Magic, spell.Id);
        }

        // spell::CanUseSpell for a PC: a trust, the canUseSpellOverride, then the main job (or the sub job unless
        // main-job-only) at its level with the spell's requirements; a Monipulator casts with its species' jobs
        private bool CharCanUseSpell(CharData c, Spell spell)
        {
            if (spell.Group == Spell.GroupTrust) return true;
            if (CallOptionalGlobalBool("xi.spells.canUseSpellOverride", c, spell)) return true;
            byte req = spell.Requirements;
            byte mjob = c.MJob, sjob = c.SJob;
            if (c.Monstrosity is { } mon) (mjob, sjob) = (mon.MainJob, mon.SubJob);
            var fx = c.StatusEffects;
            bool Usable(int job)
            {
                if ((req & SpellReqTabulaRasa) != 0 && !fx.HasStatusEffect(MagicFx.TabulaRasa)) return false;
                if (job == JobSch)
                {
                    if ((req & SpellReqAddendumBlack) != 0)
                    {
                        if (!fx.HasStatusEffect(MagicFx.AddendumBlack, MagicFx.Enlightenment)) return false;
                    }
                    else if ((req & SpellReqAddendumWhite) != 0 && !fx.HasStatusEffect(MagicFx.AddendumWhite, MagicFx.Enlightenment)) return false;
                }
                if (spell.Group == Spell.GroupBlue)
                {
                    if ((req & SpellReqUnbridledLearning) != 0)
                    {
                        if (!fx.HasStatusEffect(MagicFx.UnbridledLearning, MagicFx.UnbridledWisdom)) return false;
                    }
                    else if (GetBlueSpell(spell.Id) is not BlueSpell blue || !IsBlueSpellSet(c, blue)) return false;
                }
                return true;
            }
            if (c.MLvl >= spell.Job(mjob) && Usable(mjob)) return true;
            return c.SLvl >= spell.Job(sjob) && (req & SpellReqMainJobOnly) == 0 && Usable(sjob);
        }

        // CMagicState::HasCost: a char's ninjutsu needs its tool (a mob's none), the rest the MP
        private bool HasMagicCost(BattleEntity caster, MagicCastState st)
        {
            var spell = st.Spell;
            if (spell.Group == Spell.GroupNinjutsu)
            {
                if (caster is CharData c && (st.Flags & MagicFlagIgnoreTools) == 0 && !HasNinjaTool(c, spell, false))
                {
                    st.ErrorMsg = SelfPackets.BattleMessage(c, spell.Id, 0, MsgNoNinjaTools);
                    return false;
                }
                return true;
            }
            if (CanAffordSpell(caster, spell, st.Flags)) return true;
            if (caster is MobEntity m && m.MaxMp == 0) log_($"CMagicState::ValidCast Mob ({m.Id}) tried to cast magic with no mp!");
            st.ErrorMsg = SelfPackets.BattleMessage(caster.Id, caster.TargId, caster.Id, caster.TargId, spell.Id, 0, MsgNotEnoughMp);
            return false;
        }

        // CMagicState::SpendCost: a ninjutsu's tool, else the MP (RDM's Quick Magic job points off an instant cast;
        // Conserve MP may refund some)
        private void SpendMagicCost(BattleEntity caster, MagicCastState st)
        {
            var spell = st.Spell;
            if (spell.Group == Spell.GroupNinjutsu)
            {
                if ((st.Flags & MagicFlagIgnoreTools) == 0 && caster is CharData nc) HasNinjaTool(nc, spell, true);
                return;
            }
            if (!spell.HasMpCost || caster.StatusEffects.HasStatusEffect(MagicFx.Manafont) || (st.Flags & MagicFlagIgnoreMp) != 0) return;
            short cost = (short)CalculateSpellCost(caster, spell);
            if (caster is CharData c && st.InstantCast)
                cost = (short)(cost * (1.0f - (float)(JobPointUtil.Value(c, JpQuickMagicEffect) * 2 / 100)));
            if (MobRng.Next(100) < caster.GetMod(Mod.CONSERVE_MP)) cost = (short)(cost * ((8.0f + (float)MobRng.NextDouble() * 8.0f) / 16.0f));
            BattleAddMp(caster, -cost);
        }

        // battleutils::HasNinjaTool: the spell's tool (its MP cost names it) in the inventory, or a NIN's universal
        // tool for its wheel; consuming one, Futae takes two of the elemental wheel's, else Ninja Tool Expertise or
        // NINJA_TOOL may save it
        private bool HasNinjaTool(CharData c, Spell spell, bool consume)
        {
            ushort toolId = spell.MpCost;
            var item = SearchItem(c, Container.Inventory, toolId);
            if (item == null)
            {
                if (c.MJob != MobStats.JobNin) return false;
                toolId = toolId switch
                {
                    1161 or 1164 or 1167 or 1170 or 1173 or 1176 => 2971,                  // the elemental wheel: Inoshishinofuda
                    2644 or 2970 or 2553 or 2642 or 1194 or 1179 or 8803 or 8804 => 2972,  // the enhancing ones: Shikanofuda
                    2555 or 1191 or 1185 or 1182 or 1188 or 2643 => 2973,                  // the enfeebling ones: Chonofuda
                    _ => (ushort)0,
                };
                if (toolId == 0 || (item = SearchItem(c, Container.Inventory, toolId)) == null) return false;
            }
            if (!consume) return true;
            bool wheel = toolId is 1161 or 1164 or 1167 or 1170 or 1173 or 1176;
            uint take;
            if (c.StatusEffects.HasStatusEffect(MagicFx.Futae) && wheel) take = 2;
            else
            {
                int meritBonus = CharStats.HasTrait(c, TraitNinjaToolExpert) ? c.Merits?.Value(c, MeritNinjaToolExpertise) ?? 0 : 0;
                int chance = c.GetMod(Mod.NINJA_TOOL) + meritBonus;
                if (MobRng.Next(100) < chance) return true;
                take = 1;
            }
            using var t = new ItemTransaction(this, c);
            if (!t.Take(Container.Inventory, item.Slot, take) || !t.Commit())
            {
                log_($"battleutils: {c.Name} could not spend the tool{(take == 2 ? "s" : "")} in slot {item.Slot}");
                return false;
            }
            PushTo(c, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
            return true;
        }

        // CMagicState::GetRecast
        private long GetMagicRecastMs(BattleEntity caster, MagicCastState st)
        {
            var fx = caster.StatusEffects;
            if (fx.HasStatusEffect(MagicFx.Chainspell) || fx.HasStatusEffect(MagicFx.Spontaneity) || st.InstantCast) return 0;
            return CalculateSpellRecastTime(caster, st.Spell);
        }

        // CMagicState::TryInterrupt via CBattleEntity::TryHitInterrupt: a hit may break a cast
        private void TryHitInterrupt(BattleEntity defender, BattleEntity attacker)
        {
            if (defender?.MagicState is { } st && attacker != null && TryInterruptSpell(attacker, defender, st.Spell))
                st.Interrupted = true;
        }

        // ---- the interrupts -----------------------------------------------------------------------------------------

        // CBattleEntity::OnCastInterrupted: the interrupt packet, the "is interrupted" message unless blocked, then a
        // mob's onSpellInterrupted and the controller's OnCastStopped (CMobEntity) or a char's error message
        // (CCharEntity)
        private void OnCastInterrupted(BattleEntity caster, MagicCastState st, ushort msg, bool blocked, long now)
        {
            var spell = st.Spell;
            MagicInterruptPacket(caster, spell);
            if (!blocked)
            {
                var msgTarget = st.Target ?? caster;
                PushCasterInRange(caster, SelfPackets.BattleMessage(caster.Id, caster.TargId, msgTarget.Id, msgTarget.TargId, 0, 0, msg));
            }
            if (caster is MobEntity m)
            {
                CallMobHook(m, "onSpellInterrupted", m, spell);
                OnCastStopped(m, now);
            }
            else if (caster is CharData c && st.ErrorMsg != null)
            {
                PushTo(c, st.ErrorMsg);
                st.ErrorMsg = null;
            }
        }

        // ActionInterrupts::MagicInterrupt
        private void MagicInterruptPacket(BattleEntity caster, Spell spell)
        {
            var interrupt = new ActionPacket { ActorId = caster.Id, ActionType = ActionCategory.MagicStart, ActionId = (uint)spell.FourCCOf(true) };
            interrupt.AddTarget(caster.Id).AddResult().Param = spell.Id;
            PushCasterInRange(caster, Battle2Packets.Battle2(interrupt));
        }

        // ActionInterrupts::MagicParalyzed / MagicIntimidated: the finish with the message, then the stop-casting packet
        // (an intimidated one carries the 2 s recast too)
        private void MagicStopped(BattleEntity caster, Spell spell, BattleEntity target, ushort msg, bool stopRecast)
        {
            var finish = new ActionPacket { ActorId = caster.Id, ActionType = ActionCategory.MagicFinish, ActionId = spell.Id, RecastSec = 2 };
            var r = finish.AddTarget(target.Id).AddResult();
            r.Animation = AnimSkillInterrupt;
            r.MessageId = msg;
            PushCasterInRange(caster, Battle2Packets.Battle2(finish));
            var stop = new ActionPacket { ActorId = caster.Id, ActionType = ActionCategory.MagicStart, ActionId = (uint)spell.FourCCOf(true) };
            if (stopRecast) stop.RecastSec = 2;
            var s = stop.AddTarget(caster.Id).AddResult();
            s.Animation = AnimSkillInterrupt;
            s.Param = spell.Id;
            PushCasterInRange(caster, Battle2Packets.Battle2(stop));
        }

        // battleutils::TryInterruptSpell: the level ratio (a base of 5 for a mob, 50 for anyone else), a char's skill
        // ratio and interruption merits, SPELLINTERRUPT; Aquaveil absorbs one
        private bool TryInterruptSpell(BattleEntity attacker, BattleEntity defender, Spell spell)
        {
            if (spell.Skill == Spell.SkillSinging) return false;
            if (defender.StatusEffects.HasStatusEffect(MagicFx.Manafont)) return false;
            if (defender.StatusEffects.HasStatusEffect(MagicFx.Chainspell)) return false;
            int baseRate = defender is MobEntity ? 5 : 50;
            float levelRatio = (baseRate + attacker.MLvl - defender.MLvl) / 100.0f;
            if (levelRatio < 0.01f) levelRatio = 0.01f;
            float skillRatio = 1.0f;
            int meritReduction = 0;
            if (defender is CharData c && loader_.Stats?.Growth is { } growth)
            {
                float skillCap = growth.GetMaxSkill(spell.Skill, c.MJob, c.MLvl);
                float skillLevel = Melee.GetSkill(c, spell.Skill);
                // If skill cap is 0, player may be using a spell from their subjob.
                if (skillCap == 0) skillCap = growth.GetMaxSkill(spell.Skill, c.SJob, c.MLvl);
                skillRatio = skillLevel <= 0 ? 10.0f : skillCap / skillLevel;
                meritReduction = (byte)(c.Merits?.Value(c, MeritSpellInterruptionRate) ?? 0);
            }
            float sird = (100.0f - meritReduction - defender.GetMod(Mod.SPELLINTERRUPT)) / 100.0f;
            if ((float)MobRng.NextDouble() >= levelRatio * skillRatio * sird) return false;
            var aquaveil = defender.StatusEffects.GetStatusEffect(MagicFx.Aquaveil);
            if (aquaveil == null) return true;
            if (aquaveil.Power - 1 == 0) defender.StatusEffects.DelStatusEffect(MagicFx.Aquaveil);
            else aquaveil.Power--;
            return false;
        }

        // battleutils::IsParalyzed
        private bool IsParalyzed(BattleEntity attacker) => MobRng.Next(100) < attacker.GetMod(Mod.PARALYZE);

        // battleutils::IsIntimidated: the defender's killer mod against the caster's ecosystem (a char is humanoid),
        // plus Doubt / Intimidate
        private bool IsIntimidated(BattleEntity attacker, BattleEntity defender)
        {
            if (attacker == defender) return false;
            return MobRng.Next(100) < IntimidationChance(attacker, defender);
        }

        // IsIntimidated's chance in %
        private static int IntimidationChance(BattleEntity attacker, BattleEntity defender)
        {
            var ecosystem = attacker is MobEntity am ? am.Ecosystem : attacker is CharData ac ? ac.EcoSystem : global::XI.Zone.Ecosystem.Humanoid;
            Mod? killer = ecosystem switch
            {
                global::XI.Zone.Ecosystem.Amorph => Mod.AMORPH_KILLER,
                global::XI.Zone.Ecosystem.Aquan => Mod.AQUAN_KILLER,
                global::XI.Zone.Ecosystem.Arcana => Mod.ARCANA_KILLER,
                global::XI.Zone.Ecosystem.Beast => Mod.BEAST_KILLER,
                global::XI.Zone.Ecosystem.Bird => Mod.BIRD_KILLER,
                global::XI.Zone.Ecosystem.Demon => Mod.DEMON_KILLER,
                global::XI.Zone.Ecosystem.Dragon => Mod.DRAGON_KILLER,
                global::XI.Zone.Ecosystem.Empty => Mod.EMPTY_KILLER,
                global::XI.Zone.Ecosystem.Humanoid => Mod.HUMANOID_KILLER,
                global::XI.Zone.Ecosystem.Lizard => Mod.LIZARD_KILLER,
                global::XI.Zone.Ecosystem.Luminion => Mod.LUMINION_KILLER,
                global::XI.Zone.Ecosystem.Luminian => Mod.LUMINIAN_KILLER,
                global::XI.Zone.Ecosystem.Plantoid => Mod.PLANTOID_KILLER,
                global::XI.Zone.Ecosystem.Undead => Mod.UNDEAD_KILLER,
                global::XI.Zone.Ecosystem.Vermin => Mod.VERMIN_KILLER,
                _ => null,
            };
            int killerEffect = killer is Mod k ? defender.GetMod(k) : 0;
            if (attacker.StatusEffects.GetStatusEffect(MagicFx.Doubt) is { } doubt) killerEffect += doubt.Power;
            if (attacker.StatusEffects.GetStatusEffect(MagicFx.Intimidate) is { } intimidate) killerEffect += intimidate.Power;
            return killerEffect;
        }

        // ---- the finish ---------------------------------------------------------------------------------------------

        // CBattleEntity::OnCastFinished, then CMobEntity's (the controller's OnCastStopped, TapDeaggroTime) or
        // CCharEntity's (the last-spell time, the Chain Affinity / Azure Lore and Immanence skillchains,
        // RemoveStratagems, the skill-ups)
        private void OnCastFinished(BattleEntity caster, MagicCastState st, ActionPacket action, long now)
        {
            var spell = st.Spell;
            var target = st.Target;
            var c = caster as CharData;
            // only blue spells that act as a physical WS can Trick Attack
            BattleEntity taChar = c != null && c.StatusEffects.HasStatusEffect(EffectId.TrickAttack) && spell.Group == Spell.GroupBlue &&
                                  spell.DealsDamage ? TrickAttackChar(c, target) : null;
            if (c != null) c.LastSpellFinishedMs = now; // setLastSpellFinishedTime
            if (caster is MobEntity pm) CallMobHook(pm, "onSpellPrecast", pm, spell);
            SpendMagicCost(caster, st);

            // remove effects based on spell cast first
            uint delFlags = EffectFlag.Invisible | EffectFlag.MagicBegin;
            if (spell.CanTargetEnemy) delFlags |= EffectFlag.Detectable;
            caster.StatusEffects.DelStatusEffectsByFlag(delFlags);
            caster.StatusEffects.DelStatusEffect(EffectId.Illusion);

            var (aoeType, radius) = MagicAoE(caster, spell);
            // Convergence reduces AoEs to a single target
            if (spell.Group == Spell.GroupBlue && caster.StatusEffects.HasStatusEffect(MagicFx.Convergence))
            {
                aoeType = Spell.AoeNone;
                radius = 0.0f;
            }
            // a mob's single-target spell at a char goes to the cover user that has COVER_MAGIC_AND_RANGED
            var originalTarget = target;
            bool isMagicCovered = false;
            if (aoeType != Spell.AoeRadial && aoeType != Spell.AoeConal && caster is MobEntity and not PetEntity and not TrustEntity &&
                target is CharData && CoverAbilityUser(target, caster) is CharData coverUser && coverUser.GetMod(Mod.COVER_MAGIC_AND_RANGED) == 1)
            {
                isMagicCovered = true;
                target = coverUser;
            }
            var targets = MagicTargets(caster, target, spell, aoeType, radius);

            spell.TotalTargets = (ushort)targets.Count;
            spell.PrimaryTargetId = target.Id;
            long recastMs = GetMagicRecastMs(caster, st);
            action.ActorId = caster.Id;
            action.ActionType = ActionCategory.MagicFinish;
            action.ActionId = spell.Id;
            action.RecastSec = (uint)(recastMs / 1000);

            ushort msg = 0, initialMessage = spell.Message;
            foreach (var t in targets)
            {
                spell.Message = initialMessage;
                var actionTarget = action.AddTarget(t.Id);
                var r = actionTarget.AddResult();
                r.Resolution = 0;
                r.Animation = spell.Animation;
                r.Param = 0;
                int ce = spell.CE, ve = spell.VE;
                if (spell.CanTargetEnemy && (aoeType > 0 || (spell.Flag & Spell.FlagWipeShadows) != 0))
                {
                    t.StatusEffects.DelStatusEffect(EffectId.Blink);
                    t.StatusEffects.DelStatusEffect(EffectId.CopyImage);
                }
                // IsAbsorbByShadow last: it uses a shadow up
                if (spell.Group != Spell.GroupBlue && spell.CanHitShadow && aoeType == Spell.AoeNone &&
                    (spell.Flag & Spell.FlagIgnoreShadows) == 0 && IsAbsorbByShadow(t, caster))
                {
                    msg = MsgShadowAbsorb;
                    r.Param = 1;
                    ce = ve = 0;
                }
                else
                {
                    int damage = OnSpellCast(caster, t, spell, action);
                    if (spell.Skill == Spell.SkillEnfeebling) caster.StatusEffects.DelStatusEffect(MagicFx.Saboteur);
                    msg = msg == 0 ? spell.Message : AoEVariant(spell.Message);
                    r.Modifier = (byte)spell.Modifier;
                    spell.Modifier = 0;
                    if (spell.Group == Spell.GroupBlue && spell.Element == 0 && msg == 2)
                    {
                        RecordDamage(r, 1, damage, t, spell.Critical);
                        spell.Critical = false;
                    }
                    else r.Param = damage;
                    if (!spell.TookEffect)
                    {
                        r.Resolution = 1;
                        r.Param = 0;
                        r.Knockback = 0;
                    }
                    else if (spell.Group == Spell.GroupBlue)
                    {
                        r.Knockback = (byte)Math.Clamp(CalculateKnockback(t, caster, spell, action), 0, 255);
                    }
                }

                if (r.Animation == AnimTeleport)
                {
                    // Teleport spells don't target unqualified members
                    if (spell.Message == 0)
                    {
                        r.Animation = 0; // stop target from going invisible
                        if (t != target) action.Targets.Remove(actionTarget);
                        else r.MessageId = MsgMagicNoEffect; // in anticipation of nobody having the gate crystal
                        continue;
                    }
                    // reset the no effect message above if somebody has the gate crystal
                    if (msg == MsgMagicTeleport && t != target) action.Targets[0].Results[0].MessageId = 0;
                }

                r.MessageId = msg;
                if (isMagicCovered) ((MobEntity)caster).Enmity.UpdateEnmityFromCover(originalTarget, target); // ApplyMagicCoverEnmity
                else ApplyMagicEnmity(caster, st, t, ce, ve, now);
                if (t is MobEntity tm && msg != MsgShadowAbsorb) CallMobHook(tm, "onMagicHit", caster, t, spell);
                // The entity under consideration for RoE objective credit: trusts credit their master
                if ((caster is TrustEntity { PMaster: CharData master } ? master : c) is CharData eminence)
                    MagicRoeEvents(eminence, t, spell, r.Param);
                if (caster is MobEntity km)
                {
                    if (target.Id == t.Id) EnmityHandleKillshot(km, target);
                    else if (km.Enmity.Get(target.Id) != null) km.Enmity.SetActive(target.Id, target.IsAlive); // handleSecondaryTargetEnmity
                }
                if (t is CharData pc && SessionOf(pc) is { } s) SendUpdateMask(s, pc);
            }

            // magic claims a living mob of another allegiance for anyone but a mob or an automaton (summoning magic
            // never); a pet's or trust's claim goes to its master (ClaimMob)
            bool isMob = caster is MobEntity and not PetEntity and not TrustEntity;
            if ((!spell.IsHeal || spell.TookEffect) && target.IsAlive && target is MobEntity claimed and not PetEntity and not TrustEntity &&
                claimed.Allegiance != EnmityContainer.AllegianceOf(caster) && spell.Skill != Spell.SkillSummoning && !isMob &&
                caster is not AutomatonEntity)
                ClaimMob(claimed, caster, now);

            ProcessActionEffectFlags(caster, action);
            caster.StatusEffects.DelStatusEffectsByFlag(EffectFlag.MagicEnd);
            var fx = caster.StatusEffects;
            if (spell.Skill == Spell.SkillDivine && fx.HasStatusEffect(MagicFx.DivineEmblem)) fx.DelStatusEffect(MagicFx.DivineEmblem);
            if (spell.Skill == Spell.SkillEnfeebling && fx.HasStatusEffect(MagicFx.Stymie)) fx.DelStatusEffect(MagicFx.Stymie);
            if (spell.Skill == Spell.SkillElemental && fx.HasStatusEffect(MagicFx.Cascade))
            {
                fx.DelStatusEffect(MagicFx.Cascade);
                caster.Tp = 0;
            }
            if (spell.Skill == Spell.SkillSinging && fx.HasStatusEffect(MagicFx.Marcato)) fx.DelStatusEffect(MagicFx.Marcato);
            if (spell.Group == Spell.GroupBlack && fx.HasStatusEffect(MagicFx.Ebullience)) fx.DelStatusEffect(MagicFx.Ebullience);
            RecastsOf(caster).Add(RecastType.Magic, spell.Id, recastMs);

            if (caster is MobEntity mob)
            {
                OnCastStopped(mob, now);
                mob.DeaggroTimeMs = now; // TapDeaggroTime
                if (mob is AutomatonEntity automaton) AutomatonOnCastFinished(automaton, spell, target);
            }
            else if (c != null)
            {
                SpellSkillchains(c, spell, target, action, taChar);
                RemoveStratagems(c, spell);
                if (spell.TookEffect)
                {
                    TrySkillUp(c, spell.Skill, target.MLvl, false, false);
                    // an instrument or a handbell in the ranged slot skills up with its songs / geomancy
                    if (c.Equipped[CharStats.SlotRanged] != null && c.RangedWeapon?.Equip != null)
                    {
                        byte itemSkill = c.RangedWeapon.Skill;
                        if (spell.Skill == Spell.SkillGeomancy && itemSkill == Spell.SkillHandbell ||
                            spell.Skill == Spell.SkillSinging && itemSkill is Spell.SkillString or Spell.SkillWind or Spell.SkillSinging)
                            TrySkillUp(c, itemSkill, target.MLvl, false, false);
                    }
                }
            }
        }

        // the CTargetFind search the finish runs: the area or cone around the target, else the target alone (the
        // dead too for a spell that may target them)
        private List<BattleEntity> MagicTargets(BattleEntity caster, BattleEntity target, Spell spell, byte aoeType, float radius)
        {
            byte findFlags = (spell.ValidTargets & TargetPlayerDead) != 0 ? FindFlagsDead : (byte)0;
            var find = new SkillTargetFind(this, caster);
            if (aoeType == Spell.AoeRadial) find.WithinArea(target, 2, radius, spell.ValidTargets, findFlags);
            else if (aoeType == Spell.AoeConal) find.WithinCone(target, radius, 45f, spell.ValidTargets, 0, findFlags);
            else find.SingleTarget(target, spell.ValidTargets, findFlags);
            return find.Targets;
        }

        // CMagicState::ApplyEnmity: Tranquility / Equanimity / Divine Benison lower it for the cast, Subtle Sorcery
        // takes the CE, Yonin raises Utsusemi's, Divine Emblem raises a divine spell's; a hostile mob target gets it
        // (and is claimed if it died: an AoE), an ally's cast goes to the mobs that hate it. A mob caster gives none
        // to its foes unless charmed (a pet or a trust does).
        private void ApplyMagicEnmity(BattleEntity caster, MagicCastState st, BattleEntity target, int ce, int ve, long now)
        {
            var spell = st.Spell;
            var fx = caster.StatusEffects;
            bool applied = false;
            if (fx.GetStatusEffect(MagicFx.Tranquility) is { } tq && spell.Group == Spell.GroupWhite) caster.AddModifier(Mod.ENMITY, (short)-tq.Power);
            if (fx.GetStatusEffect(MagicFx.Equanimity) is { } eq && spell.Group == Spell.GroupBlack) caster.AddModifier(Mod.ENMITY, (short)-eq.Power);
            if (spell.IsNa) caster.AddModifier(Mod.ENMITY, (short)-(caster.GetMod(Mod.DIVINE_BENISON) >> 1));
            if (fx.HasStatusEffect(MagicFx.SubtleSorcery)) ce = 0;
            if (fx.HasStatusEffect(MagicFx.Yonin) && spell.Family == FamilyUtsusemi && caster.GetMod(Mod.YONIN_UTSUSEMI_ENMITY) > 0)
            {
                ce = 160;
                ve = 480;
            }
            if (fx.GetStatusEffect(MagicFx.DivineEmblem) is { } emblem && spell.Skill == Spell.SkillDivine)
            {
                ve = (int)(ve * (1.0f + emblem.Power / 100.0f));
                ce = (int)(ce * (1.0f + emblem.Power / 100.0f));
            }

            if (target != null)
            {
                int casterAllegiance = EnmityContainer.AllegianceOf(caster);
                if (target is MobEntity mob && mob.Allegiance != casterAllegiance)
                {
                    if (!mob.IsAlive) mob.DropItemTimeMs = spell.AnimationTimeMs;
                    // As in C++, these returns leave the modifiers above in place.
                    if (spell.Group == Spell.GroupSummoning) return;
                    if (!spell.IsHeal || spell.TookEffect) // can't claim mob with cure unless it does damage
                    {
                        // a mob (not a pet or trust) gives none, unless charmed
                        bool isMob = caster is MobEntity and not PetEntity and not TrustEntity;
                        if (isMob && !caster.IsCharmed) return;
                        mob.Enmity.UpdateEnmity(caster, ce, ve);
                        applied = true;
                        if (!mob.IsAlive) ClaimMob(mob, caster, now); // claim mob only on death (for aoe)
                        DirtyExp(mob, caster);
                    }
                }
                else if (EnmityContainer.AllegianceOf(target) == casterAllegiance)
                {
                    GenerateInRangeEnmity(caster, ce, ve);
                    applied = true;
                }
            }

            if (fx.GetStatusEffect(MagicFx.Tranquility) is { } tq2 && spell.Group == Spell.GroupWhite)
            {
                caster.DelModifier(Mod.ENMITY, (short)-tq2.Power);
                if (applied) fx.DelStatusEffect(MagicFx.Tranquility);
            }
            if (fx.GetStatusEffect(MagicFx.Equanimity) is { } eq2 && spell.Group == Spell.GroupBlack)
            {
                caster.DelModifier(Mod.ENMITY, (short)-eq2.Power);
                if (applied) fx.DelStatusEffect(MagicFx.Equanimity);
            }
            if (spell.IsNa) caster.DelModifier(Mod.ENMITY, (short)-(caster.GetMod(Mod.DIVINE_BENISON) >> 1));
            if (fx.HasStatusEffect(MagicFx.DivineEmblem) && spell.Skill == Spell.SkillDivine && applied) fx.DelStatusEffect(MagicFx.DivineEmblem);
        }

        // battleutils::GenerateInRangeEnmity: the mobs near the char (or its master) that already hate the source
        private void GenerateInRangeEnmity(BattleEntity source, int ce, int ve)
        {
            var pc = source as CharData ?? source.PMaster as CharData;
            if (pc == null) return;
            foreach (var id in new List<uint>(pc.SpawnMobList))
                if (MobById(id) is { } m && m.HiPcLvl > 0 && m.Enmity.HasId(source.Id))
                    m.Enmity.UpdateEnmity(source, ce, ve, false);
        }

        // battleutils::DirtyExp: the attacker's alliance near the mob counts toward its EXP party size and level
        private void DirtyExp(MobEntity mob, BattleEntity attacker)
        {
            var pc = attacker as CharData ?? attacker.PMaster as CharData;
            if (pc == null) return;
            int pcInZone = 0;
            byte maxLevel = 0;
            foreach (var member in ForAlliance(pc))
            {
                if (!SameArea(member, mob) || Distance(member, mob) >= 100) continue;
                maxLevel = Math.Max(maxLevel, member.MLvl);
                pcInZone++;
            }
            mob.HiPartySize = (byte)Math.Max(pcInZone, mob.HiPartySize);
            mob.HiPcLvl = Math.Max(maxLevel, mob.HiPcLvl);
        }

        // the RoE objective credit for a cast on oneself or a party / alliance member: a heal (and a unity ally's), or
        // a buff that landed on someone else
        private void MagicRoeEvents(CharData c, BattleEntity t, Spell spell, int param)
        {
            var tc = t as CharData;
            bool ally = t == c || (tc != null && c.Party != null && tc.Party != null &&
                                   (c.Party == tc.Party || (c.Party.Alliance != null && c.Party.Alliance == tc.Party.Alliance)));
            if (!ally) return;
            if (spell.IsHeal)
            {
                RoeEvent(RoeEventHealAlly, c, new (string, object)[] { ("heal", param) });
                if (tc != null && tc != c && c.UnityLeader == tc.UnityLeader)
                    RoeEvent(RoeEventHealUnityAlly, c, new (string, object)[] { ("heal", param) });
            }
            else if (t != c && spell.IsBuff && param != 0)
            {
                RoeEvent(RoeEventBuffAlly, c, Array.Empty<(string, object)>());
            }
        }

        private const ushort EffectAzureLore = 163, EffectChainAffinity = 164, EffectImmanence = 470;
        private const ushort FamilyFire = 42, FamilyBlizzard = 43, FamilyAero = 44, FamilyStone = 45, FamilyThunder = 46,
            FamilyWater = 47, FamilyGeohelix = 76, FamilyHydrohelix = 77, FamilyAnemohelix = 78, FamilyPyrohelix = 79,
            FamilyCryohelix = 80, FamilyIonohelix = 81, FamilyNoctohelix = 82, FamilyLuminohelix = 83;

        // CCharEntity::OnCastFinished's skillchains, on every result against the spell's target: a damaging blue spell
        // under Chain Affinity or Azure Lore with its properties (Chain Affinity then takes the TP, or 1000 of it under
        // Sekkanoki / Meikyo Shisui, and wears off), and a damaging black spell of an element under Immanence (which it
        // uses up; a helix closing a skillchain gives the burst window 2 s more)
        private void SpellSkillchains(CharData c, Spell spell, BattleEntity target, ActionPacket action, BattleEntity taChar)
        {
            var fx = c.StatusEffects;
            foreach (var actionTarget in action.Targets)
            {
                foreach (var r in actionTarget.Results)
                {
                    if (r.Param > 0 && spell.DealsDamage && spell.Group == Spell.GroupBlue &&
                        (fx.HasStatusEffect(EffectChainAffinity) || fx.HasStatusEffect(EffectAzureLore)) &&
                        GetBlueSpell(spell.Id) is { PrimarySc: not 0 } blue)
                    {
                        byte effect = GetSkillChainEffect(target, blue.PrimarySc, blue.SecondarySc, 0);
                        if (effect != ProcScNone) RecordSkillchain(r, effect, TakeSkillchainDamage(c, target, r.Param, taChar));

                        // only Chain Affinity reduces TP
                        if (fx.HasStatusEffect(EffectChainAffinity))
                        {
                            if (fx.HasStatusEffect(EffectSekkanoki) || fx.HasStatusEffect(EffectMeikyoShisui)) c.Tp = c.Tp > 1000 ? c.Tp - 1000 : 0;
                            else c.Tp = 0;
                            fx.DelStatusEffectSilent(EffectChainAffinity);
                        }
                    }

                    // Immanence will create or extend a skillchain for elemental spells
                    if (target.Hp > 0 && r.Param >= 0 && spell.DealsDamage && spell.Group == Spell.GroupBlack && fx.HasStatusEffect(EffectImmanence))
                    {
                        bool isHelix = spell.Family is >= FamilyGeohelix and <= FamilyLuminohelix;
                        byte element = spell.Family switch
                        {
                            FamilyStone or FamilyGeohelix => ScScission,
                            FamilyWater or FamilyHydrohelix => ScReverberation,
                            FamilyAero or FamilyAnemohelix => ScDetonation,
                            FamilyFire or FamilyPyrohelix => ScLiquefaction,
                            FamilyBlizzard or FamilyCryohelix => ScInduration,
                            FamilyThunder or FamilyIonohelix => ScImpaction,
                            FamilyNoctohelix => ScCompression,
                            FamilyLuminohelix => ScTransfixion,
                            _ => ScNone,
                        };
                        if (element == ScNone) continue;
                        fx.DelStatusEffect(EffectImmanence);
                        byte effect = GetSkillChainEffect(target, element, 0, 0);
                        if (effect == ProcScNone) continue;
                        RecordSkillchain(r, effect, TakeSkillchainDamage(c, target, r.Param));

                        // Closing a skillchain with an immanence Helix will make the magic burst window longer
                        if (isHelix && target.StatusEffects.GetStatusEffect(EffectSkillchain, 0) is { } sc) sc.DurationMs += 2000;
                    }
                }
            }
        }

        // charutils::RemoveStratagems: the arts' stratagems a white or black spell uses up
        private static void RemoveStratagems(CharData c, Spell spell)
        {
            var fx = c.StatusEffects;
            if (spell.Group == Spell.GroupWhite)
            {
                // rapture to be deleted in applicable scripts
                fx.DelStatusEffect(MagicFx.Penury);
                fx.DelStatusEffect(MagicFx.Celerity);
                fx.DelStatusEffect(MagicFx.Enlightenment);
                fx.DelStatusEffect(MagicFx.Altruism);
                fx.DelStatusEffect(MagicFx.Tranquility);
                if (spell.Aoe == Spell.AoeRadialAcce) fx.DelStatusEffect(MagicFx.Accession);
                if (spell.Skill == Spell.SkillEnhancing) fx.DelStatusEffect(MagicFx.Perpetuance);
            }
            else if (spell.Group == Spell.GroupBlack)
            {
                // ebullience to be deleted in applicable scripts
                fx.DelStatusEffect(MagicFx.Parsimony);
                fx.DelStatusEffect(MagicFx.Alacrity);
                fx.DelStatusEffect(MagicFx.Enlightenment);
                fx.DelStatusEffect(MagicFx.Focalization);
                fx.DelStatusEffect(MagicFx.Equanimity);
                if (spell.Aoe == Spell.AoeRadialMani) fx.DelStatusEffect(MagicFx.Manifestation);
            }
        }

        // ---- the formulas -------------------------------------------------------------------------------------------

        // battleutils::CalculateSpellCastTime: a char's merits and job points too. The C++ works on timer::duration
        // (steady_clock nanoseconds): a product with a float or double is a float / double count of nanoseconds,
        // floored to milliseconds (std::chrono::floor), so the arithmetic below is done the same way (NsTimesF /
        // NsTimesD); the result is in milliseconds.
        private long CalculateSpellCastTime(BattleEntity m, MagicCastState st)
        {
            var spell = st.Spell;
            var fx = m.StatusEffects;
            var c = m as CharData;
            if (MobRng.Next(100) < m.GetMod(Mod.QUICK_MAGIC))
            {
                st.InstantCast = true;
                return 0;
            }
            long baseCast = spell.CastTimeMs * NsPerMs;
            long cast = baseCast;
            if (fx.HasStatusEffect(MagicFx.Hasso) || fx.HasStatusEffect(MagicFx.Seigan)) cast = NsTimesD(cast, 1.5);

            if (spell.Group == Spell.GroupBlack)
            {
                if (fx.HasStatusEffect(MagicFx.Alacrity)) cast -= NsTimesF(baseCast, (50 + ArtsBonus(m, spell)) / 100.0f);
                else if (spell.Skill == Spell.SkillDark)
                    cast = NsTimesF(cast, 1.0f + (m.GetMod(Mod.BLACK_MAGIC_CAST) + m.GetMod(Mod.DARK_MAGIC_CAST)) / 100.0f);
                else if (fx.HasStatusEffect(MagicFx.DarkArts) || fx.HasStatusEffect(MagicFx.AddendumBlack))
                    cast = NsTimesF(cast, 1.0f + (m.GetMod(Mod.BLACK_MAGIC_CAST) + m.GetMod(Mod.GRIMOIRE_SPELLCASTING)) / 100.0f);
                else cast = NsTimesF(cast, 1.0f + m.GetMod(Mod.BLACK_MAGIC_CAST) / 100.0f);
            }
            else if (spell.Group == Spell.GroupWhite)
            {
                if (fx.HasStatusEffect(MagicFx.Celerity)) cast -= NsTimesF(baseCast, (50 + ArtsBonus(m, spell)) / 100.0f);
                else if (fx.HasStatusEffect(MagicFx.LightArts) || fx.HasStatusEffect(MagicFx.AddendumWhite))
                    cast = NsTimesF(cast, 1.0f + (m.GetMod(Mod.WHITE_MAGIC_CAST) + m.GetMod(Mod.GRIMOIRE_SPELLCASTING)) / 100.0f);
                else cast = NsTimesF(cast, 1.0f + m.GetMod(Mod.WHITE_MAGIC_CAST) / 100.0f);
            }
            else if (spell.Group == Spell.GroupSummoning)
            {
                long amount = 1000L * NsPerMs * m.GetMod(Mod.SUMMONING_MAGIC_CAST);
                if (c != null && ChatCfg.EnableSmnMagicCastTimeMerit)
                    amount += NsTimesD(baseCast, 0.01 * (c.Merits?.Value(c, MeritSummoningMagicCastTime) ?? 0));
                cast = cast > amount ? cast - Math.Max(amount, 0) : 0;
            }
            else if (spell.Group == Spell.GroupSong)
            {
                if (fx.HasStatusEffect(MagicFx.Pianissimo) && spell.Aoe == Spell.AoePianissimo) cast = baseCast / 2;
                if (fx.HasStatusEffect(MagicFx.Nightingale))
                {
                    if (c != null && MobRng.Next(100) < (c.Merits?.Value(c, MeritNightingale) ?? 0) - 25) return 0;
                    cast = NsTimesF(cast, 0.5f);
                }
                if (fx.HasStatusEffect(MagicFx.Troubadour)) cast = NsTimesF(cast, 1.5f);
                int songCast = Math.Min((int)(ushort)m.GetMod(Mod.SONG_SPELLCASTING_TIME), 50);
                cast = NsTimesF(cast, 1.0f - songCast / 100.0f);
            }
            else if (spell.Group == Spell.GroupNinjutsu && c != null)
            {
                cast = NsTimesF(cast, 1.0f - 0.03f * JobPointUtil.Value(c, JpNinjutsuCastTimeBonus));
            }

            int fastCast = Math.Clamp((int)m.GetMod(Mod.FASTCAST), -100, 50);
            if (spell.Skill == Spell.SkillElemental) fastCast += m.GetMod(Mod.ELEMENTAL_CELERITY);
            else if (spell.IsCure)
            {
                fastCast += m.GetMod(Mod.CURE_CAST_TIME);
                if (c != null) fastCast += c.Merits?.Value(c, MeritCureCastTime) ?? 0;
            }
            else if (spell.Skill == Spell.SkillGeomancy && c != null) fastCast += JobPointUtil.Value(c, JpWidenedCompassEffect);
            fastCast = Math.Clamp(fastCast, -100, 80);
            int uncapped = Math.Clamp((int)m.GetMod(Mod.UFASTCAST), -100, 100);
            int inspiration = Math.Clamp((int)m.GetMod(Mod.INSPIRATION_FAST_CAST), -100, 100);
            if (spell.IsNa) uncapped = Math.Clamp(uncapped + m.GetMod(Mod.DIVINE_BENISON), -100, 100);
            float sum = Math.Clamp((float)(fastCast + uncapped + inspiration), -100f, 100f);
            return NsTimesF(cast, (100.0f - sum) / 100.0f) / NsPerMs;
        }

        private const long NsPerMs = 1_000_000;

        // std::chrono::floor<milliseconds>(d * f) of a nanosecond duration and a float: the float count of
        // nanoseconds, cast to whole milliseconds (float division, truncation), one less when that is past the product;
        // in nanoseconds
        private static long NsTimesF(long ns, float f)
        {
            float d = ns * f;
            long t = (long)(d / 1000000f);
            if (t * 1000000f > d) t--;
            return t * NsPerMs;
        }

        // the same with a double
        private static long NsTimesD(long ns, double f)
        {
            double d = ns * f;
            long t = (long)(d / 1000000.0);
            if (t * 1000000.0 > d) t--;
            return t * NsPerMs;
        }

        // Alacrity / Celerity: ALACRITY_CELERITY_EFFECT when the weather matches the spell's element, and SCH's
        // Stratagem II job points
        private int ArtsBonus(BattleEntity b, Spell spell)
        {
            int bonus = WeatherMatchesElement(BattleWeather(b), spell.Element) ? b.GetMod(Mod.ALACRITY_CELERITY_EFFECT) : 0;
            if (b is CharData c) bonus += JobPointUtil.Value(c, JpStratagemEffectII);
            return (ushort)bonus;
        }

        // battleutils::GetWeather(entity, false)
        private ushort BattleWeather(BattleEntity b) => Weather(b, false, AreaOf(b)?.Weather ?? WeatherNone);

        // battleutils::WeatherMatchesElement: either weather of the element (fire hot spell / heat wave .. dark gloom /
        // darkness)
        private static bool WeatherMatchesElement(ushort weather, byte element) => element switch
        {
            1 => weather is 4 or 5,
            2 => weather is 12 or 13,
            3 => weather is 10 or 11,
            4 => weather is 8 or 9,
            5 => weather is 14 or 15,
            6 => weather is 6 or 7,
            7 => weather is 16 or 17,
            8 => weather is 18 or 19,
            _ => false,
        };

        // battleutils::CalculateSpellCost
        private int CalculateSpellCost(BattleEntity b, Spell spell)
        {
            if (!spell.HasMpCost) return 0;
            ushort baseCost = spell.MpCost;
            if (spell.Id == Spell.Embrava || spell.Id == Spell.Kaustra) baseCost = (ushort)(b.MaxMp * 0.2);
            short cost = (short)baseCost;
            bool applyArts = true;
            var fx = b.StatusEffects;
            if (spell.Group == Spell.GroupBlack)
            {
                if (spell.Aoe == Spell.AoeRadialMani && fx.HasStatusEffect(MagicFx.Manifestation))
                {
                    cost *= 2;
                    applyArts = false;
                }
                if (fx.HasStatusEffect(MagicFx.Parsimony)) cost /= 2;
                else if (applyArts) cost += (short)(baseCost * (b.GetMod(Mod.BLACK_MAGIC_COST) / 100.0f));
            }
            else if (spell.Group == Spell.GroupWhite)
            {
                if (spell.Aoe == Spell.AoeRadialAcce && fx.HasStatusEffect(MagicFx.Accession))
                {
                    cost *= 2;
                    applyArts = false;
                }
                if (fx.HasStatusEffect(MagicFx.Penury)) cost /= 2;
                else if (applyArts) cost += (short)(baseCost * (b.GetMod(Mod.WHITE_MAGIC_COST) / 100.0f));
            }
            int reduction = b.GetMod(Mod.MP_COST_REDUCTION);
            if (reduction > 0) cost = (short)(cost * (1.0f - reduction / 100.0f));
            if (MobRng.Next(100) < b.GetMod(Mod.NO_SPELL_MP_DEPLETION)) cost = 0;
            return Math.Clamp((int)cost, 0, 9999);
        }

        // battleutils::CanAffordSpell
        private bool CanAffordSpell(BattleEntity b, Spell spell, byte flags)
        {
            if (b.StatusEffects.HasStatusEffect(MagicFx.Manafont) || (flags & MagicFlagIgnoreMp) != 0) return true;
            if (b is MobEntity m && m.GetMobMod(MobMod.NoSpellCost) > 0) return true;
            return !spell.HasMpCost || b.Mp >= CalculateSpellCost(b, spell);
        }

        // battleutils::CalculateSpellRecastTime, on nanoseconds as the C++ timer::duration (NsTimesF / NsTimesD); the
        // result is in milliseconds
        private long CalculateSpellRecastTime(BattleEntity b, Spell spell)
        {
            var fx = b.StatusEffects;
            long baseRecast = spell.RecastTimeMs * NsPerMs;
            long recast = baseRecast;
            float cap = ChatCfg.SpellRecastReductionCap;
            long CapFloor(float c) => NsTimesF(baseRecast, 1.0f - c / 100.0f);

            float fastCast = Math.Clamp(b.GetMod(Mod.FASTCAST) / 2.0f, 0f, 40f);
            float inspiration = b.GetMod(Mod.INSPIRATION_FAST_CAST) / 2.0f;
            recast = NsTimesF(recast, (100.0f - (fastCast + inspiration)) / 100.0f);

            int hasteMagic = Math.Clamp((int)b.GetMod(Mod.HASTE_MAGIC), -10000, 4375);
            int hasteGear = Math.Min((int)b.GetMod(Mod.HASTE_GEAR), 2500);
            recast = NsTimesF(recast, (10000.0f - (hasteMagic + hasteGear)) / 10000.0f);

            if (spell.Group == Spell.GroupSong)
            {
                if (fx.HasStatusEffect(MagicFx.Nightingale)) recast = NsTimesF(recast, 0.5f);
                // the following modifiers are not multiplicative - as such they must be applied last
                if (b is CharData c)
                {
                    if (spell.Id == SpellMagicFinale) recast -= (c.Merits?.Value(c, MeritFinaleRecast) ?? 0) * 1000L * NsPerMs;
                    if (spell.Id is SpellFoeLullaby or SpellFoeLullabyII or SpellHordeLullaby or SpellHordeLullabyII)
                        recast -= (c.Merits?.Value(c, MeritLullabyRecast) ?? 0) * 1000L * NsPerMs;
                }
                recast -= b.GetMod(Mod.SONG_RECAST_DELAY) * 1000L * NsPerMs;
            }
            if (fx.HasStatusEffect(MagicFx.Composure)) recast = NsTimesF(recast, 1.25f);
            if (fx.HasStatusEffect(MagicFx.Hasso) || fx.HasStatusEffect(MagicFx.Seigan)) recast = NsTimesF(recast, 1.5f);
            recast = Math.Max(recast, CapFloor(cap));

            Mod? skillRecast = spell.Skill switch
            {
                Spell.SkillElemental => Mod.ELEMENTAL_MAGIC_RECAST,
                Spell.SkillBlue => Mod.BLUE_MAGIC_RECAST,
                Spell.SkillHealing => Mod.HEALING_MAGIC_RECAST,
                Spell.SkillEnfeebling => Mod.ENFEEBLING_MAGIC_RECAST,
                Spell.SkillEnhancing => Mod.ENHANCING_MAGIC_RECAST,
                _ => null,
            };
            recast = NsTimesF(recast, (100.0f + (skillRecast is Mod sr ? b.GetMod(sr) : 0)) / 100.0f);

            if (spell.Group == Spell.GroupBlack)
            {
                if (spell.Aoe == Spell.AoeRadialMani && fx.HasStatusEffect(MagicFx.Manifestation))
                    recast *= b.MJob == JobSch ? 2 : 3;
                else if (fx.HasStatusEffect(MagicFx.DarkArts) || fx.HasStatusEffect(MagicFx.AddendumBlack))
                    recast = NsTimesF(recast, (100.0f + b.GetMod(Mod.BLACK_MAGIC_RECAST) + b.GetMod(Mod.GRIMOIRE_SPELLCASTING)) / 100.0f);
                else recast = NsTimesF(recast, (100.0f + b.GetMod(Mod.BLACK_MAGIC_RECAST)) / 100.0f);
                recast = Math.Max(recast, CapFloor(cap));
                if (fx.HasStatusEffect(MagicFx.Alacrity))
                {
                    recast = NsTimesD(recast, 0.60); // 40% reduction from Alacrity alone
                    float reductionCap = cap;
                    // the relic feet bonus only applies when the spell element matches the weather, and only then does
                    // the cap extend to 90%
                    if (WeatherMatchesElement(BattleWeather(b), spell.Element) && (ushort)b.GetMod(Mod.ALACRITY_CELERITY_EFFECT) is var bonus && bonus > 0)
                    {
                        recast = NsTimesF(recast, (100 - bonus) / 100.0f);
                        reductionCap = cap + 10.0f;
                    }
                    recast = Math.Max(recast, CapFloor(reductionCap));
                }
            }
            else if (spell.Group == Spell.GroupWhite)
            {
                if (spell.Aoe == Spell.AoeRadialAcce && fx.HasStatusEffect(MagicFx.Accession))
                    recast *= b.MJob == JobSch ? 2 : 3;
                if (fx.HasStatusEffect(MagicFx.LightArts) || fx.HasStatusEffect(MagicFx.AddendumWhite))
                    recast = NsTimesF(recast, (100.0f + b.GetMod(Mod.WHITE_MAGIC_RECAST) + b.GetMod(Mod.GRIMOIRE_SPELLCASTING)) / 100.0f);
                else recast = NsTimesF(recast, (100.0f + b.GetMod(Mod.WHITE_MAGIC_RECAST)) / 100.0f);
                recast = Math.Max(recast, CapFloor(cap));
                if (fx.HasStatusEffect(MagicFx.Celerity))
                {
                    recast = NsTimesD(recast, 0.60); // 40% reduction from Celerity alone
                    float reductionCap = cap;
                    // the relic feet bonus only applies when the spell element matches the weather, and only then does
                    // the cap extend to 90%
                    if (WeatherMatchesElement(BattleWeather(b), spell.Element) && (ushort)b.GetMod(Mod.ALACRITY_CELERITY_EFFECT) is var bonus && bonus > 0)
                    {
                        recast = NsTimesF(recast, (100 - bonus) / 100.0f);
                        reductionCap = cap + 10.0f;
                    }
                    recast = Math.Max(recast, CapFloor(reductionCap));
                }
            }
            return Math.Max(recast, 0) / NsPerMs;
        }

        // ---- the scripts --------------------------------------------------------------------------------------------

        // luautils::OnMagicCastingCheck: the spell script's check; 0 lets the cast start. No script refuses it (47).
        private int OnMagicCastingCheck(BattleEntity caster, BattleEntity target, Spell spell)
        {
            if (lua_ == null) return 0;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(spell.ScriptFile, "onMagicCastingCheck");
                if (lua_.IsNil(-1))
                {
                    log_($"luautils::onMagicCastingCheck, spell ({spell.Name}) has no lua file, it cannot be cast.");
                    return 47;
                }
                lua_.Push(caster);
                lua_.Push(target);
                lua_.Push(spell);
                lua_.Call(3, 1);
                return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : -5;
            }
            catch (LuaException e)
            {
                log_($"luautils::onMagicCastingCheck ({spell.Name}): {e.Message}");
                return 47;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnSpellCast: the spell script's effect, its number the damage (or the amount) shown
        private int OnSpellCast(BattleEntity caster, BattleEntity target, Spell spell, ActionPacket action)
        {
            if (lua_ == null) return 0;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(spell.ScriptFile, "onSpellCast");
                if (lua_.IsNil(-1)) return 0;
                lua_.Push(caster);
                lua_.Push(target);
                lua_.Push(spell);
                lua_.Push(action);
                lua_.Call(4, 1);
                return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : 0;
            }
            catch (LuaException e)
            {
                log_($"luautils::onSpellCast: {e.Message}");
                return 0;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnMobSpellChoose: the mob script may name another spell and / or another target
        private (ushort?, BattleEntity) OnMobSpellChoose(MobEntity m, BattleEntity target, ushort? spellId)
        {
            if (lua_ == null) return (null, null);
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(MobScriptFile(m), "onMobSpellChoose");
                if (lua_.IsNil(-1)) return (null, null);
                lua_.Push(m);
                lua_.Push(target);
                if (spellId is ushort id) lua_.Push((double)id);
                else lua_.PushNil();
                lua_.Call(3, 2);
                ushort? spell = lua_.IsNumber(-2) ? (ushort)(int)lua_.ToIntegral(-2) : null; // get<int32>, then SpellID
                var newTarget = lua_.ToObject(-1) as BattleEntity;
                return (spell, newTarget);
            }
            catch (LuaException e)
            {
                log_($"luautils::OnMobSpellChoose: {e.Message}");
                return (null, null);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // xi.combat.magicAoE.calculateTypeAndRadius(caster, spell): { aoe type, radius }
        private (byte, float) MagicAoE(BattleEntity caster, Spell spell)
        {
            const string path = "xi.combat.magicAoE.calculateTypeAndRadius";
            if (lua_ == null) return (0, 0f);
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (lua_.IsNil(-1))
                {
                    log_($"luautils::callGlobalFunction: {path}: Function not found");
                    return (0, 0f);
                }
                lua_.Push(caster);
                lua_.Push(spell);
                lua_.Call(2, 1);
                if (!lua_.IsTable(-1)) return (0, 0f);
                lua_.RawGetI(-1, 1);
                byte aoe = lua_.IsNumber(-1) ? (byte)lua_.ToEnum(-1) : (byte)0; // get_or(1, SPELLAOE_NONE): an enum
                lua_.Pop(1);
                lua_.RawGetI(-1, 2);
                float radius = lua_.IsNumber(-1) ? (float)lua_.ToNumber(-1) : 0f;
                return (aoe, radius);
            }
            catch (LuaException e)
            {
                log_($"luautils::callGlobalFunction: {path}: {e.Message}");
                return (0, 0f);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // a global boolean function that may not exist (luautils::OnCanUseSpell): false when it is missing or errs,
        // its boolean when it returns one, else true
        private bool CallOptionalGlobalBool(string path, params object[] args)
        {
            if (lua_ == null) return false;
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (!lua_.IsFunction(-1)) return false;
                foreach (var a in args) lua_.Push(a);
                lua_.Call(args.Length, 1);
                return lua_.Type(-1) != 1 || lua_.ToBoolean(-1); // LUA_TBOOLEAN
            }
            catch (LuaException e)
            {
                log_($"luautils::OnCanUseSpell: {e.Message}");
                return false;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // a global number function that may not exist (sol's valid() check): null when it is missing
        private int? CallOptionalGlobalInt(string path, params object[] args)
        {
            if (lua_ == null) return null;
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (!lua_.IsFunction(-1)) return null;
                foreach (var a in args) lua_.Push(a);
                lua_.Call(args.Length, 1);
                return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : 0;
            }
            catch (LuaException e)
            {
                log_($"lua: {path}: {e.Message}");
                return null;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // CBattleEntity::addTP for either kind
        private void BattleAddTp(BattleEntity b, int tp)
        {
            if (b is CharData c) AddTp(c, (short)tp);
            else AddMobTp((MobEntity)b, (short)tp);
        }

        // battleutils::TakeSpellDamage: the damage, then for a hostile spell that hurt the damage-flag effects wear off
        // and both sides gain TP by the scripts' formulas
        private void TakeSpellDamage(BattleEntity defender, BattleEntity attacker, Spell spell, int damage, byte attackType, byte damageType)
        {
            // Scarlet Delirium: Updates status effect power with damage bonus
            Melee.HandleScarletDelirium(defender, damage);
            BattleTakeDamage(defender, damage, attacker, attackType, damageType);
            if (!spell.CanTargetEnemy || damage <= 0) return;
            defender.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Damage);
            // Check for bind breaking
            BindBreakCheck(defender);
            if (CallOptionalGlobalInt("xi.combat.tp.calculateSpellTP", attacker, spell) is int spellTp) BattleAddTp(attacker, spellTp);
            if (CallOptionalGlobalInt("xi.combat.tp.calculateTPGainOnMagicalDamage", attacker, defender, damage) is int gain) BattleAddTp(defender, gain);
        }

        // ---- the bindings -------------------------------------------------------------------------------------------

        private void RegisterMagicBindings(Dictionary<string, LuaFunction> methods)
        {
            RegisterSpellType();
            // castSpell([spell], [target]): queued; a mob's recast of it is dropped, then the cast on the target (or a
            // mob's battle target); a char casts through CPlayerController::Cast. No spell lets a mob's controller pick
            // one (TryCastSpell).
            methods["castSpell"] = l =>
            {
                if (Entity(l) is not BattleEntity caster) return 0;
                if (l.IsNil(2))
                {
                    QueueNative(caster, 0, true, _ =>
                    {
                        if (caster is MobEntity pm) TryCastSpell(pm, NowMs());
                    });
                    return 0;
                }
                ushort id = (ushort)l.ToEnum(2); // as<SpellID>
                var target = l.ToObject(3) as BattleEntity;
                QueueNative(caster, 0, true, _ =>
                {
                    if (caster is CharData c)
                    {
                        if (target != null) PlayerCast(c, target, id);
                        return;
                    }
                    if (caster is not MobEntity mob) return;
                    RecastsOf(mob).Del(RecastType.Magic, id);
                    var castTarget = target ?? mob.BattleTarget;
                    if (castTarget != null) MobCast(mob, castTarget, id, NowMs());
                });
                return 0;
            };
            methods["takeSpellDamage"] = l =>
            {
                if (Entity(l) is not BattleEntity defender)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                if (l.ToObject(2) is not BattleEntity attacker)
                {
                    log_($"Invalid entity type passed as Attacker ({NameOf(Entity(l))}).");
                    return 0;
                }
                if (l.ToObject(3) is not Spell spell) return 0;
                TakeSpellDamage(defender, attacker, spell, l.ToIntArg(4), (byte)l.ToIntArg(5), (byte)l.ToIntArg(6));
                return 0;
            };
            methods["hasSpellList"] = l =>
            {
                if (Entity(l) is MobEntity mob) return Ret(l, mob.SpellContainer.HasSpells);
                log_($"function call on invalid entity! (name: {NameOf(Entity(l))} type: {Entity(l)?.GetType().Name})");
                return Ret(l, false);
            };
            methods["getSpellListId"] = l =>
            {
                if (Entity(l) is MobEntity mob)
                {
                    if (mob.SpellList == null) return Ret(l, 0);
                    if (mob.SpellList.Id is ushort listId) return Ret(l, listId);
                    log_($"CLuaBaseEntity::getSpellListId: {mob.Name} names its own spells and has no list id");
                    return Ret(l, 0);
                }
                log_($"function call on invalid entity! (name: {NameOf(Entity(l))} type: {Entity(l)?.GetType().Name})");
                return Ret(l, 0);
            };
            methods["tryHitInterrupt"] = l =>
            {
                if (Battle(l) is { } defender && l.ToObject(2) is BattleEntity attacker) TryHitInterrupt(defender, attacker);
                return 0;
            };
        }
    }
}
