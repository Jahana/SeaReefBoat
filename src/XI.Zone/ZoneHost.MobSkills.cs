using System;
using System.Collections.Generic;
using XI.Map;
using XI.Script;

namespace XI.Zone
{
    // CMobSkill (battleutils' mob_skills / mob_skill_lists, each skill's ./scripts/actions/mobskills script loaded the
    // first time the skill is asked for) and the mob side of using one (M7): CMobController's TP move in DoCombatTick
    // (shouldUseTPMove, MobSkill(listId) with onMobMobskillChoose, TryMobSkill with onMobSkillTarget,
    // onMobSkillCheck and onMobSkillReadyTime), useMobAbility, CMobSkillState (the "readies" 0x028, the TP spent,
    // facing the target through the ready, the finish packet then the animation time, the interrupt packet when it
    // never lands, WEAPONSKILL_STATE_ENTER / EXIT, onMobSkillFinalize, setFinalAnimationSub), and
    // CBattleEntity::OnMobSkillFinished (out of range, CTargetFind's single / area / cone search and target
    // validity, ALWAYS_ANIMATE, each target's knockback, onMobWeaponSkill (the mob script's, then the skill's),
    // WEAPONSKILL_USE / TAKE, the AoE message variants, recovered HP, recordDamage, shadows and misses, the killshot
    // and secondary-target enmity, processActionEffectFlags, TapDeaggroTime), and the mob's own effects on it
    // (amnesia / impairment refuse it, Sekkanoki and Meikyo Shisui in the cost, a prevent-action effect or hysteria
    // at the finish interrupts it, with the TP refund). The attack state's swing countdown stands still while a skill
    // state sits above it. The finish records the skill in m_UsedSkillIds (a BLU learns from them at the kill,
    // ZoneHost.BlueLearning.cs), lets a self buff take the mob's pets (FINDFLAGS_PET), sends an avatar's as a pet skill
    // and turns a single-target skill at a char onto its cover user; the target search knows pets, charm and battle
    // ids (SkillTargetFind), and a pet's skill runs its pet or automaton script.
    public sealed class MobSkill
    {
        public const ushort FlagAstralFlow = 0x002, FlagNoTpCost = 0x004, FlagNoStartMsg = 0x010, FlagNoFinishMsg = 0x020,
                            FlagBloodPactRage = 0x040, FlagBloodPactWard = 0x080, FlagAlwaysAnimate = 0x100;

        // CPetSkill: a pet_skills row rides on the same class (IsPetSkill), with the mob skill a jug pet's move shows
        // and its finish category
        public bool IsPetSkill;
        public ushort MobSkillId;
        public byte FinishCategory;

        public ushort Id, AnimationId, ValidTargets, Flag;
        public string Name = "";
        public byte Aoe, Knockback, PrimarySc, SecondarySc, TertiarySc;
        public float AoeRadius, Distance;
        public long AnimationTimeMs, ActivationTimeMs;
        public short Param;
        public ushort Msg = 185;

        // what one use carries (the state's copy)
        public List<BattleEntity> Targets = new List<BattleEntity>();
        public ushort TotalTargets = 1;
        public uint PrimaryTargetId;
        public int Tp, Hp;
        public byte Hpp;
        public byte? FinalAnimationSub;
        public byte AttackType; // xi::AttackType: 0 none, 1 physical, 2 magical, 3 ranged, 4 breath, 5 special
        public bool Critical;

        public bool IsAoE => Aoe > 0 && Aoe < 4;
        public bool IsConal => Aoe == 4 || Aoe == 8 && !IsPetSkill;
        public bool IsSingle => Aoe == 0;
        public bool IsAstralFlow => (Flag & FlagAstralFlow) != 0;
        public bool IsTpFree => (Flag & FlagNoTpCost) != 0;
        public bool IsBloodPactRage => (Flag & FlagBloodPactRage) != 0;
        public bool IsBloodPactWard => (Flag & FlagBloodPactWard) != 0;
        public bool HasMissMsg => IsPetSkill ? Msg is 324 or 158 or 188 or 189 or 31 or 30 : Msg is 158 or 188 or 189 or 31 or 30 or 354;

        // CPetSkill::isDamageMsg
        public bool IsDamageMsg => Msg is 110 or 185 or 187 or 197 or 225 or 226 or 264 or 317;

        // getRadius: a mob skill's radius of 0 is its distance (8 around a target-centered one); a pet skill's is as is
        public float Radius => IsPetSkill ? AoeRadius : AoeRadius <= 0 ? (Aoe == 2 ? 8 : Distance) : AoeRadius;

        public MobSkill Clone()
        {
            var c = (MobSkill)MemberwiseClone();
            c.Targets = new List<BattleEntity>();
            return c;
        }
    }

    // CMobSkillState: on top of the mob's AI while the skill readies and lands
    public sealed class MobSkillState
    {
        public MobSkill Skill;
        public BattleEntity Target;
        public long EntryMs, CastMs, FinishMs;
        public int SpentTp;
        public bool Completed, Success;
        public long SwingRemainingMs; // the attack state's countdown, held while this state runs
        public ushort? TpCostOverride; // a Monstrosity move's fixed cost, paid when it fires
    }

    public sealed partial class ZoneHost
    {
        private const string MobSkillType = "CMobSkill";
        private const int MaxMobSkillId = 4262;
        private const ushort TargetNpc = 0x40, TargetAnyAllegiance = 0x800;
        private const ushort BehaviorRaisable = 0x004, BehaviorNoAssist = 0x008;
        private const ushort MsgSkillReadies = 43, MsgNoTargetInArea = 76, MsgSkillRecoversHp = 103, MsgShadowAbsorb = 31,
                             MsgTargetEvades = 282;
        private const ushort AnimSkillInterrupt = 508;
        private const ushort EffectAmnesia = 16, EffectMeikyoShisui = 54, EffectImpairment = 261, EffectSekkanoki = 408, EffectHysteria = 804;
        private const byte AoeAttacker = 1, AoeConalRear = 8;

        private Dictionary<ushort, MobSkill> mobSkills_;
        private Dictionary<ushort, List<ushort>> mobSkillLists_;
        private readonly HashSet<string> mobSkillScripts_ = new HashSet<string>(StringComparer.Ordinal);

        // battleutils::LoadMobSkillsList
        private void LoadMobSkills()
        {
            if (mobSkills_ != null) return;
            mobSkills_ = new Dictionary<ushort, MobSkill>();
            mobSkillLists_ = new Dictionary<ushort, List<ushort>>();
            var r = Exec("SELECT mob_skill_id, mob_anim_id, mob_skill_name, mob_skill_aoe, mob_skill_aoe_radius, mob_skill_distance, " +
                         "mob_anim_time, mob_prepare_time, mob_valid_targets, mob_skill_flag, mob_skill_param, knockback, " +
                         "primary_sc, secondary_sc, tertiary_sc FROM mob_skills");
            while (r != null && r.Next())
            {
                ushort id = r.Get<ushort>("mob_skill_id");
                if (id >= MaxMobSkillId)
                {
                    log_($"mob_skill_id {id} is out of range, MAX_MOBSKILL_ID is {MaxMobSkillId}. Skipping.");
                    continue;
                }
                var s = new MobSkill
                {
                    Id = id,
                    AnimationId = r.Get<ushort>("mob_anim_id"),
                    Name = r.Get<string>("mob_skill_name"),
                    Aoe = r.Get<byte>("mob_skill_aoe"),
                    AoeRadius = r.Get<float>("mob_skill_aoe_radius"),
                    Distance = r.Get<float>("mob_skill_distance"),
                    AnimationTimeMs = r.Get<ushort>("mob_anim_time"),
                    ActivationTimeMs = r.Get<ushort>("mob_prepare_time"),
                    ValidTargets = r.Get<ushort>("mob_valid_targets"),
                    Flag = r.Get<ushort>("mob_skill_flag"),
                    Param = r.Get<short>("mob_skill_param"),
                    Knockback = (byte)r.Get<sbyte>("knockback"),
                    PrimarySc = (byte)r.Get<sbyte>("primary_sc"),
                    SecondarySc = (byte)r.Get<sbyte>("secondary_sc"),
                    TertiarySc = (byte)r.Get<sbyte>("tertiary_sc"),
                };
                if ((s.ValidTargets & TargetSelf) != 0 && (s.ValidTargets & TargetEnemy) != 0)
                    log_($"Mob skill {s.Name} ({id}) has both TARGET_SELF and TARGET_ENEMY set");
                mobSkills_[id] = s;
            }
            r = Exec("SELECT skill_list_id, mob_skill_id FROM mob_skill_lists");
            while (r != null && r.Next())
            {
                ushort list = r.Get<ushort>("skill_list_id");
                if (!mobSkillLists_.TryGetValue(list, out var ids)) mobSkillLists_[list] = ids = new List<ushort>();
                ids.Add(r.Get<ushort>("mob_skill_id"));
            }
        }

        // battleutils::GetMobSkill, with the skill's script loaded on first use
        private MobSkill GetMobSkill(int id)
        {
            LoadMobSkills();
            if (id < 0 || id > ushort.MaxValue || !mobSkills_.TryGetValue((ushort)id, out var s)) return null;
            string file = $"./scripts/actions/mobskills/{s.Name}.lua";
            if (scripts_ != null && mobSkillScripts_.Add(file)) scripts_.LoadLuaObjectFromFile(file);
            return s;
        }

        private List<ushort> GetMobSkillList(int listId)
        {
            LoadMobSkills();
            return mobSkillLists_.TryGetValue((ushort)listId, out var ids) ? new List<ushort>(ids) : new List<ushort>();
        }

        // ---- the controller -----------------------------------------------------------------------------------------

        // CMobController::shouldUseTPMove
        private bool ShouldUseTpMove(MobEntity m)
        {
            if (m.Tp < 1000 || !m.MobAbilityEnabled || GetMobSkillList(m.GetMobMod(MobMod.SkillList)).Count == 0) return false;
            if (m.Tp == 3000 || (m.Hpp < 25 && m.Tp >= 1000)) return true;
            return m.Tp >= m.TpThreshold;
        }

        // CMobController::MobSkill(listId): the list shuffled, the mob script's pick, else the first that can go
        private bool MobSkillFromList(MobEntity m, int listId)
        {
            if (m.BattleTarget is not { } target) return false;
            int resolved = listId != 0 ? listId : m.GetMobMod(MobMod.SkillList);
            var list = GetMobSkillList(resolved);
            list.RemoveAll(id =>
            {
                if (GetMobSkill(id) != null) return false;
                log_($"Mobskill with ID ({id}) [called from skill-list ID ({resolved})] isn't properly defined in mob_skills.sql");
                return true;
            });
            if (list.Count == 0) return false;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = MobRng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            int chosen = OnMobMobskillChoose(m, target, list[0]);
            if (chosen > 0) return TryMobSkill(m, (ushort)chosen, target);
            foreach (var id in list)
                if (TryMobSkill(m, id, target)) return true;
            return false;
        }

        // CMobController::TryMobSkill
        private bool TryMobSkill(MobEntity m, ushort id, BattleEntity target)
        {
            var skill = GetMobSkill(id);
            if (skill == null || skill.IsAstralFlow) return false;
            BattleEntity actionTarget = (skill.ValidTargets & TargetEnemy) != 0 ? target : (skill.ValidTargets & TargetSelf) != 0 ? m : null;
            actionTarget = OnMobSkillTarget(actionTarget, m, skill);
            if (actionTarget == null) return false;
            if (OnMobSkillCheck(actionTarget, m, skill) != 0) return false;
            if (Distance(m, actionTarget) > skill.Distance) return false;
            return MobUseSkill(m, actionTarget, id, OnMobSkillReadyTime(actionTarget, m, skill), NowMs());
        }

        // CMobController::MobSkill(target, id, cast): face it, WEAPONSKILL_BEFORE_USE, then Internal_MobSkill
        private bool MobUseSkill(MobEntity m, BattleEntity target, ushort id, long? castMs, long now)
        {
            FaceTarget(m, target);
            TriggerListener(m, "WEAPONSKILL_BEFORE_USE", m, (int)id);
            if (IsUntargetable(target)) return false;
            if (!CanChangeState(m)) return false;
            return StartMobSkill(m, target, id, castMs, now);
        }

        // ---- the state ----------------------------------------------------------------------------------------------

        // CMobSkillState::init (a mob's, a pet's or a Monipulator's)
        private bool StartMobSkill(BattleEntity m, BattleEntity target, ushort id, long? castMs, long now, ushort? tpCostOverride = null)
        {
            var template = GetMobSkill(id);
            if (template == null) return false;
            if (m.StatusEffects.HasStatusEffect(EffectAmnesia, EffectImpairment)) return false;
            bool selfCentered = template.Aoe == AoeAttacker;
            if (!ValidSkillTarget(selfCentered ? m : target, m, selfCentered ? TargetSelf : template.ValidTargets)) return false;

            var st = new MobSkillState { Skill = template.Clone(), Target = target, EntryMs = now, TpCostOverride = tpCostOverride };
            st.CastMs = castMs ?? st.Skill.ActivationTimeMs;
            st.SwingRemainingMs = Math.Max(0, m.SwingAtMs - now);
            m.SkillState = st;
            if (st.CastMs > 0)
            {
                // a self buff readies on the mob; a self-centered move on its battle target
                BattleEntity shown = template.ValidTargets == TargetSelf ? m : selfCentered ? SkillBattleTarget(m) : target;
                uint shownId = (shown ?? m).Id;
                if (m is not CharData && ChatCfg.HideReadiesTarget) shownId = m.Id;
                var readies = new ActionPacket { ActorId = m.Id, ActionType = ActionCategory.SkillStart, ActionId = (uint)FourCC.SkillUse };
                var r = readies.AddTarget(shownId).AddResult();
                r.Param = st.Skill.Id;
                r.MessageId = (st.Skill.Flag & MobSkill.FlagNoStartMsg) != 0 ? (ushort)0 : MsgSkillReadies;
                PushSkillInRange(m, Battle2Packets.Battle2(readies));
                FaceSkillTarget(m, target);
            }
            TriggerListener(m, "WEAPONSKILL_STATE_ENTER", m, (int)st.Skill.Id);
            SpendMobSkillCost(m, st);
            if (st.CastMs == 0 && UpdateMobSkill(m, now)) EndMobSkill(m, now);
            return true;
        }

        // CMobSkillState::SpendCost: a Monstrosity move's fixed cost waits for the finish; Sekkanoki spends 1000 and wears
        // off; Meikyo Shisui counts its free moves down
        private void SpendMobSkillCost(BattleEntity m, MobSkillState st)
        {
            if (st.Skill.IsTpFree) return;
            if (st.TpCostOverride is ushort cost) st.SpentTp = cost;
            else if (m.StatusEffects.HasStatusEffect(EffectSekkanoki))
            {
                int before = m.Tp;
                if (m is MobEntity mob) AddMobTp(mob, -1000);
                else m.Tp = Math.Max(0, m.Tp - 1000);
                st.SpentTp = Math.Abs(before - m.Tp);
                m.StatusEffects.DelStatusEffect(EffectSekkanoki);
            }
            else if (m.StatusEffects.HasStatusEffect(EffectMeikyoShisui) &&
                     m.LocalVars.TryGetValue("[MeikyoShisui]MobSkillCount", out uint count) && count > 0)
            {
                m.LocalVars["[MeikyoShisui]MobSkillCount"] = count - 1;
                st.SpentTp = 3000; // "Unknown how mobs behave like this"
                if (count - 1 == 0) m.Tp = 0;
            }
            else
            {
                st.SpentTp = m.Tp;
                m.Tp = 0;
            }
        }

        // the state's turn each AI tick: done once the animation time after the finish has passed
        private void TickMobSkill(BattleEntity m, long now)
        {
            if (m.SkillState != null && UpdateMobSkill(m, now)) EndMobSkill(m, now);
        }

        // CMobSkillState::Update: true when the state is done
        private bool UpdateMobSkill(BattleEntity m, long now)
        {
            var st = m.SkillState;
            if (st == null) return false;
            st.Success = false;
            if (st.CastMs > 0 && now < st.EntryMs + st.CastMs) FaceSkillTarget(m, st.Target);
            if (m.IsAlive && now >= st.EntryMs + st.CastMs && !st.Completed)
            {
                // stunned, asleep, hysteric... at the finish: done uncompleted, the cleanup interrupts
                if (m.StatusEffects.HasPreventActionEffect() || m.StatusEffects.HasStatusEffect(EffectHysteria)) return true;
                // JP wiki: Meikyo Shisui makes one Monstrosity move free; else the TP must still cover it
                if (st.TpCostOverride.HasValue && st.SpentTp > 0)
                {
                    if (m.StatusEffects.HasStatusEffect(EffectMeikyoShisui)) m.StatusEffects.DelStatusEffect(EffectMeikyoShisui);
                    else if (m.Tp < st.SpentTp)
                    {
                        if (m is CharData pc) PushTo(pc, SelfPackets.BattleMessage(pc, 0, 0, MsgUnableToUseWeaponskill));
                        return true;
                    }
                    else m.Tp -= st.SpentTp;
                }
                var action = new ActionPacket();
                if (st.Skill.IsPetSkill && m is PetEntity pet) OnPetSkillFinished(pet, st, action, now);
                else OnMobSkillFinished(m, st, action, now);
                if (!st.Skill.IsPetSkill && (st.Skill.Flag & MobSkill.FlagNoFinishMsg) != 0)
                    foreach (var t in action.Targets)
                        foreach (var r in t.Results)
                            r.MessageId = 0;
                if (action.Targets.Count != 0)
                {
                    st.Success = true;
                    PushSkillInRange(m, Battle2Packets.Battle2(action));
                    if (m is CharData { Monstrosity: not null } mon) MonstrosityMobSkillFired(mon, st.Skill.Id);
                }
                if (m is CharData skiller && SessionOf(skiller) is { } ss) SendUpdateMask(ss, skiller);
                lua_?.Forget(action);
                st.FinishMs = now + st.Skill.AnimationTimeMs;
                st.Completed = true;
            }
            if (st.Completed && now > st.FinishMs)
            {
                // a hit on a mob from the player side draws its enmity, a pet's or a charmed mob's bringing its master
                // in at base enmity (a pet skill's is CPetSkillState's, PetSkillDone). As in C++ the success flag is
                // reset on each update, so this holds only when the finish and the end fall in one update.
                if (st.Success && st.Target is MobEntity tm && tm != m && EnmityContainer.AllegianceOf(m) == CharData.AllegiancePlayer &&
                    !(st.Skill.IsPetSkill && m is PetEntity))
                    tm.Enmity.UpdateEnmity(m, 0, 0, withMaster: m is PetEntity || m is MobEntity && m.IsCharmed);
                if (st.Skill.IsPetSkill && m is PetEntity pet) PetSkillDone(pet, st, now);
                return true;
            }
            return false;
        }

        // the state finishing (resumeNextState): its cleanup, then the attack state picks its countdown up again
        private void EndMobSkill(BattleEntity m, long now)
        {
            var st = m.SkillState;
            if (st == null) return;
            CleanupMobSkill(m, st);
            if (m.SkillState == st) m.SkillState = null;
            m.SwingAtMs = now + st.SwingRemainingMs;
        }

        // CMobSkillState::Cleanup: the interrupt packet if it never landed, else the final animation sub and
        // onMobSkillFinalize; then WEAPONSKILL_STATE_EXIT while alive
        private void CleanupMobSkill(BattleEntity m, MobSkillState st)
        {
            if (st.Skill.IsPetSkill)
            {
                // CPetSkillState::Cleanup: the interrupt, else the final animation sub (no finalize for pets)
                if (!st.Completed) AbilityInterrupt(m);
                else if (m.IsAlive && st.Skill.FinalAnimationSub is byte petSub && m is MobEntity petMob)
                {
                    petMob.AnimationSub = petSub;
                    m.UpdateMask |= EntityUpdatePacket.UpdateCombat;
                }
            }
            else if (!st.Completed)
            {
                AbilityInterrupt(m);
                // reduceTpOnInterrupt: a prevent-action effect gives a third back from 2900, else a quarter
                if (m.IsAlive && m.StatusEffects.HasPreventActionEffect() && !m.StatusEffects.HasStatusEffect(EffectMeikyoShisui))
                    m.Tp = st.SpentTp >= 2900 ? (int)MathF.Round(st.SpentTp / 3.0f) : (int)MathF.Floor(0.25f * st.SpentTp);
            }
            else
            {
                if (m.IsAlive && st.Skill.FinalAnimationSub is byte sub && m is MobEntity subMob)
                {
                    subMob.AnimationSub = sub;
                    m.UpdateMask |= EntityUpdatePacket.UpdateCombat;
                }
                OnMobSkillFinalize(m, st.Skill);
            }
            if (m.IsAlive) TriggerListener(m, "WEAPONSKILL_STATE_EXIT", m, (int)st.Skill.Id, st.Completed);
            lua_?.Forget(st.Skill);
        }

        // ActionInterrupts::AbilityInterrupt
        private void AbilityInterrupt(BattleEntity m)
        {
            var interrupt = new ActionPacket { ActorId = m.Id, ActionType = ActionCategory.SkillStart, ActionId = (uint)FourCC.SkillInterrupt };
            interrupt.AddTarget(m.Id).AddResult();
            PushSkillInRange(m, Battle2Packets.Battle2(interrupt));
        }

        // ClearStateStack / a forced despawn: the skill state cleaned up where it stands
        private void AbortMobSkill(BattleEntity m, long now)
        {
            if (m.SkillState != null) EndMobSkill(m, now);
        }

        // ---- the finish ---------------------------------------------------------------------------------------------

        // CMobEntity::OnMobSkillFinished (CBattleEntity's, then a mob's TapDeaggroTime)
        private void OnMobSkillFinished(BattleEntity m, MobSkillState st, ActionPacket action, long now)
        {
            var skill = st.Skill;
            var target = st.Target;
            if (target == null || !EntityExists(target))
            {
                log_("CMobEntity::OnMobSkillFinished: PTarget is null");
                return;
            }
            // forced interrupt via status effect (charm included)
            if (m.StatusEffects.HasPreventActionEffect(false))
            {
                AbilityInterrupt(m);
                return;
            }
            // store the skill used
            if (m is MobEntity used) used.UsedSkillIds[skill.Id] = m.MLvl;
            var find = new SkillTargetFind(this, m);
            byte findFlags = 0;
            // Mob buff abilities also hit monster's pets
            if (skill.ValidTargets == TargetSelf) findFlags |= FindFlagsPet;
            if ((skill.ValidTargets & TargetIgnoreBattleId) == TargetIgnoreBattleId) findFlags |= FindFlagsIgnoreBattleId;
            action.ActorId = m.Id;
            action.ActionType = m is PetEntity { PetType: PetType.Avatar } ? ActionCategory.PetSkillFinish
                : skill.Id < 256 ? ActionCategory.SkillFinish : ActionCategory.MobSkillFinish;
            action.ActionId = skill.Id;
            bool selfCentered = skill.Aoe == AoeAttacker;
            if (!selfCentered && Distance(m, target) > skill.Distance)
            {
                // ActionInterrupts::MobSkillOutOfRange
                var oor = new ActionPacket { ActorId = m.Id, ActionType = ActionCategory.MagicFinish };
                var r = oor.AddTarget(target.Id).AddResult();
                r.Animation = AnimSkillInterrupt;
                r.MessageId = MsgTooFarAway;
                PushSkillInRange(m, Battle2Packets.Battle2(oor));
                TapDeaggroTime(m, now);
                return;
            }
            if (skill.IsAoE)
            {
                var findTarget = selfCentered && (skill.ValidTargets & TargetEnemy) != 0 ? SkillBattleTarget(m) ?? target : target;
                find.WithinArea(findTarget, skill.Aoe, skill.Radius, skill.ValidTargets, findFlags);
            }
            else if (skill.IsConal)
            {
                find.WithinCone(target, skill.Distance, 45f, skill.ValidTargets, skill.Aoe, findFlags);
            }
            else
            {
                // a single-target skill at a char lands on its cover user
                if (m is MobEntity and not PetEntity and not TrustEntity && target is CharData && CoverAbilityUser(target, m) is { } coverUser)
                    target = coverUser;
                find.SingleTarget(target, skill.ValidTargets, findFlags);
            }

            if (find.Targets.Count == 0)
            {
                if ((skill.Flag & MobSkill.FlagAlwaysAnimate) != 0)
                {
                    var r = action.AddTarget(m.Id).AddResult();
                    r.MessageId = 0;
                    r.Animation = skill.AnimationId;
                    r.Resolution = 1;
                    r.Info = 4; // ActionInfo::UnknownAoE
                }
                else
                {
                    // ActionInterrupts::MobSkillNoTargetInRange
                    var none = new ActionPacket { ActorId = m.Id, ActionType = ActionCategory.MagicFinish };
                    var r = none.AddTarget(m.Id).AddResult();
                    r.Animation = AnimSkillInterrupt;
                    r.MessageId = MsgNoTargetInArea;
                    PushSkillInRange(m, Battle2Packets.Battle2(none));
                }
                TapDeaggroTime(m, now);
                return;
            }

            skill.Targets = new List<BattleEntity>(find.Targets);
            skill.TotalTargets = (ushort)find.Targets.Count;
            skill.PrimaryTargetId = target.Id;
            skill.Tp = st.SpentTp;
            skill.Hp = m.Hp;
            skill.Hpp = (byte)m.Hpp;

            ushort msg = 0, defaultMessage = skill.Msg;
            bool first = true;
            void Process(BattleEntity t)
            {
                var r = action.AddTarget(t.Id).AddResult();
                r.Resolution = 0;
                r.Animation = skill.AnimationId;
                r.MessageId = skill.Msg;
                r.Knockback = (byte)Math.Clamp(CalculateKnockback(t, m, skill, action), 0, 255);
                skill.Msg = defaultMessage;
                // a char's pet (not a jug pet): the automaton's or the pet's ability script
                int damage = m switch
                {
                    AutomatonEntity automaton => OnAutomatonAbility(t, automaton, skill, m.PMaster, action),
                    PetEntity { PetType: not PetType.JugPet } pet => OnPetAbility(t, pet, skill, m.PMaster, action),
                    _ => OnMobWeaponSkill(m, t, skill, action),
                };
                TriggerListener(m, "WEAPONSKILL_USE", m, t, skill, st.SpentTp, action, damage);
                TriggerListener(t, "WEAPONSKILL_TAKE", m, t, skill, st.SpentTp, action);
                msg = msg == 0 ? skill.Msg : AoEVariant(skill.Msg);
                if (damage < 0)
                {
                    msg = MsgSkillRecoversHp;
                    r.Param = Math.Clamp(-damage, 0, Math.Max(0, t.MaxHp - t.Hp));
                }
                else
                {
                    RecordDamage(r, skill.AttackType, damage, t, skill.Critical);
                    skill.Critical = false;
                }
                r.MessageId = msg;
                if (skill.Msg == MsgShadowAbsorb)
                {
                    r.Resolution = 1;
                    r.Param = damage;
                    r.Knockback = 0;
                }
                else if (skill.HasMissMsg)
                {
                    r.Resolution = 1;
                    r.Param = 0;
                    r.Knockback = 0;
                }
                if (first)
                {
                    bool isNegated = r.Resolution == 1 || r.Resolution == 3; // Miss, Parry
                    if (!isNegated) SkillchainOnHit(m, t, r, r.Param, skill.PrimarySc, skill.SecondarySc, skill.TertiarySc);
                    first = false;
                }
                // a kill claims the mob (battleutils::ClaimMob)
                if (!t.IsAlive && t is MobEntity killed && m is CharData) ClaimMob(killed, m, now);
                if (m is MobEntity me)
                {
                    if (t.Id == target.Id)
                    {
                        // handleKillshotEnmity
                        if (!t.IsAlive && me.Enmity.GetHighestEnmity(_ => true, EnmityCharById) is { } highest && highest.Id == t.Id) me.Enmity.Clear(t.Id);
                    }
                    else if (me.Enmity.Get(t.Id) is { } entry)
                    {
                        // handleSecondaryTargetEnmity
                        entry.Active = t.IsAlive;
                    }
                }
                if (t is CharData pc && SessionOf(pc) is { } s) SendUpdateMask(s, pc);
            }
            if (find.Targets.Contains(m)) Process(m);
            foreach (var t in find.Targets)
                if (t != m) Process(t);
            ProcessActionEffectFlags(m, action);
            TapDeaggroTime(m, now);
            if (m is AutomatonEntity automaton) AutomatonOnMobSkillFinished(automaton, skill, target);
        }

        private const ushort MsgUnableToUseWeaponskill = 90; // MsgBasic::UnableToUseWeaponskill

        // the battle target a skill state reads (a mob's, a char's)
        private static BattleEntity SkillBattleTarget(BattleEntity e) => e switch
        {
            MobEntity mob => mob.BattleTarget,
            CharData c => c.BattleTarget,
            _ => null,
        };

        // CHAR_INRANGE_SELF for whoever uses the skill
        private void PushSkillInRange(BattleEntity e, BasicPacket p)
        {
            if (e is CharData c) PushCharInRange(c, p, true);
            else if (e is MobEntity m) PushMobInRange(m, p);
        }

        // battleutils::turnTowardsTarget: a mob's FaceTarget, else the entity turned to it
        private static void FaceSkillTarget(BattleEntity e, BattleEntity target)
        {
            if (e is MobEntity m)
            {
                FaceTarget(m, target);
                return;
            }
            if (target == null || target == e) return;
            byte rot = WorldAngle(e.X, e.Z, e.Rotation, target.X, target.Z);
            if (rot == e.Rotation) return;
            e.Rotation = rot;
            e.UpdateMask |= EntityUpdatePacket.UpdatePos;
        }

        // CMobEntity::TapDeaggroTime
        private static void TapDeaggroTime(BattleEntity e, long now)
        {
            if (e is MobEntity m) m.DeaggroTimeMs = now;
        }

        private bool EntityExists(BattleEntity e) => e switch
        {
            CharData c => SessionOf(c) != null && c.Zone != 0,
            MobEntity mob => mob.IsSpawned,
            _ => false,
        };

        // action_result_t::recordDamage
        private static void RecordDamage(ActionResult r, byte attackType, int damage, BattleEntity target, bool critical)
        {
            r.Param = damage;
            if (target != null && !target.IsAlive) r.Info |= 1;
            if (attackType is 1 or 3 or 5)
            {
                if (critical) r.Info |= 2;
                if (target != null)
                {
                    int pct = target.MaxHp > 0 ? damage * 100 / target.MaxHp : 0;
                    r.HitDistortion = (byte)(pct >= 20 ? 3 : pct >= 10 ? 2 : pct > 0 ? 1 : 0);
                }
            }
        }

        // messageutils::GetAoEVariant
        private static readonly Dictionary<ushort, ushort> AoEVariants = new Dictionary<ushort, ushort>
        {
            [7] = 367, [93] = 273, [85] = 284, [230] = 266, [236] = 277, [237] = 278, [2] = 264, [185] = 264, [186] = 266,
            [187] = 281, [188] = 282, [189] = 283, [225] = 366, [242] = 277, [243] = 278, [370] = 404, [362] = 363,
            [224] = 276, [102] = 263, [103] = 24, [238] = 367, [306] = 24, [318] = 263, [324] = 282, [158] = 282,
            [378] = 343, [317] = 264, [131] = 286, [134] = 287, [148] = 149, [150] = 151, [319] = 266, [320] = 267,
            [323] = 283, [441] = 421, [435] = 436, [437] = 438, [439] = 440, [668] = 669, [420] = 421, [424] = 421,
            [422] = 423, [425] = 423, [426] = 427,
        };

        private static ushort AoEVariant(ushort msg) => AoEVariants.TryGetValue(msg, out var v) ? v : msg;

        // ---- validity and the target search -------------------------------------------------------------------------

        // ValidTarget(initiator = a mob, or a Monipulator) for a char or mob target
        private static bool ValidSkillTarget(BattleEntity target, BattleEntity initiator, ushort flags)
        {
            if (target == null) return false;
            int ta = EnmityContainer.AllegianceOf(target), ia = EnmityContainer.AllegianceOf(initiator);
            // CBattleEntity::ValidTarget
            bool Base()
            {
                if ((flags & TargetEnemy) != 0 && target.IsAlive && !(target is MobEntity dm && dm.IsDead))
                {
                    // a mob on its own side only with SKIP_ALLEGIANCE_CHECK; otherwise the team / nation / PvE rules
                    if (ta <= CharData.AllegiancePlayer && ia <= CharData.AllegiancePlayer && ta == ia)
                        return initiator is MobEntity im && im.GetMobMod(MobMod.SkipAllegianceCheck) == 1;
                    return BattleValidTarget((byte)ia, (byte)ta, TargetEnemy, false);
                }
                return (flags & TargetSelf) != 0 && target == initiator;
            }
            if (target is CharData c)
            {
                // belligerents can only harm each other; Monipulators and adventurers help only their own kind
                if (Monstrosity.AreBelligerents(initiator, c)) return (flags & TargetEnemy) != 0 && c.Hp > 0;
                if (ta == ia && !Monstrosity.CanAssist(initiator, c)) return false;
                if (c.Hp <= 0) return (flags & TargetPlayerDead) != 0;
                if ((flags & TargetPlayer) != 0 && ta == ia) return true;
                if (Base()) return true;
                // a pet's party skill on its master, or on its master's party
                bool isPartyPetMaster = initiator.PMaster is CharData pm && pm.Party != null && pm.Party == c.Party;
                bool isSoloPetMaster = c.Party == null && initiator.PMaster == c;
                return (flags & TargetPlayerParty) != 0 && (isPartyPetMaster || isSoloPetMaster);
            }
            // CAutomatonEntity::ValidTarget: its own TARGET_PLAYER skill on itself
            if (target is AutomatonEntity && (flags & TargetPlayer) != 0 && target == initiator) return true;
            // CPetEntity::ValidTarget: no TARGET_PLAYER skill on a pet of the initiator's side
            if (target is PetEntity && (flags & TargetPlayer) != 0 && ta == ia) return false;
            var mob = (MobEntity)target;
            if (Base()) return true;
            bool dead = !mob.IsAlive || mob.IsDead;
            if ((flags & TargetPlayerDead) != 0 && (mob.Behavior & BehaviorRaisable) != 0 && dead) return true;
            bool assists = ta == ia && (mob.Behavior & BehaviorNoAssist) == 0;
            if ((flags & TargetPlayer) != 0 && assists) return true;
            return (flags & TargetNpc) != 0 && assists;
        }

        // CTargetFind for a mob's skill
        private sealed class SkillTargetFind
        {
            private readonly ZoneHost host_;
            private readonly BattleEntity caster_;
            public readonly List<BattleEntity> Targets = new List<BattleEntity>();
            private bool conal_, selfCentered_, monsterPlayer_;
            private float radius_, cx_, cy_, cz_;
            private BattleEntity target_;
            private ushort flags_;
            private byte findFlags_;
            private float ax_, az_, bx_, bz_, ccx_, ccz_, scalar_;

            public SkillTargetFind(ZoneHost host, BattleEntity caster)
            {
                host_ = host;
                caster_ = caster;
            }

            private int CasterAllegiance => EnmityContainer.AllegianceOf(caster_);

            public void SingleTarget(BattleEntity t, ushort flags, byte findFlags = 0)
            {
                flags_ = flags;
                findFlags_ = findFlags;
                target_ = null;
                (cx_, cy_, cz_) = (t.X, t.Y, t.Z);
                Add(t);
            }

            // the objtype the pet rule compares: PC, MOB, PET or TRUST
            private static int Kind(BattleEntity e) => e switch { CharData => 0x01, PetEntity => 0x08, TrustEntity => 0x20, MobEntity => 0x02, _ => 0 };

            public void WithinArea(BattleEntity t, byte radiusType, float radius, ushort flags, byte findFlags = 0)
            {
                flags_ = flags;
                findFlags_ = findFlags;
                radius_ = radius;
                selfCentered_ = radiusType == AoeAttacker;
                if (selfCentered_) (cx_, cy_, cz_) = (caster_.X, caster_.Y, caster_.Z);
                else (cx_, cy_, cz_) = (t.X, t.Y, t.Z);
                // findMaster: a pet's master stands for it
                var master = t.PMaster ?? t;
                // no pets in an AoE buff (PETS_CAN_AOE_BUFF is false) unless forced or across sides; a pet's own
                // self-centered AoE takes no pets
                bool withPet = (findFlags_ & FindFlagsPet) != 0 || Kind(master) != Kind(caster_);
                if (selfCentered_ && caster_.PMaster != null) withPet = false;
                if (!selfCentered_ || conal_)
                {
                    Add(t);
                    target_ = t;
                }
                // checkIsPlayer: a char, or a char's pet
                if ((caster_ as CharData ?? caster_.PMaster as CharData) is CharData pc)
                {
                    if (master is CharData pm)
                    {
                        // PLAYER_PLAYER: the caster first for a self-centered one, then the party with its trusts
                        // (addAllInParty's ForPartyWithTrusts; the alliance only with FINDFLAGS_ALLIANCE)
                        if (selfCentered_) Add(caster_);
                        if (pm.Party != null)
                        {
                            if ((findFlags_ & FindFlagsAlliance) != 0 && pm.Party.Alliance != null)
                                foreach (var member in host_.ForAlliance(pm)) Add(member, withPet);
                            else
                                foreach (var member in PartyWithTrusts(pm))
                                    if (member is not CharData { MoghouseId: not 0 }) Add(member, withPet);
                        }
                        else Add(pm, withPet);
                    }
                    else
                    {
                        // PLAYER_MONSTER: addAllInMobList, the mobs the char (or the pet's master) sees that it may hit
                        foreach (var id in new List<uint>(pc.SpawnMobList))
                            if (host_.MobById(id) is { } m && host_.IsMobOwner(pc, m)) Add(m);
                    }
                    return;
                }
                var mob = (MobEntity)caster_;
                if ((flags_ & TargetAnyAllegiance) != 0)
                {
                    monsterPlayer_ = true;
                    if ((flags_ & TargetSelf) != 0 && mob.BattleTarget != null) master = mob.BattleTarget.PMaster ?? mob.BattleTarget;
                }
                else
                {
                    monsterPlayer_ = master is CharData || mob.Allegiance == CharData.AllegiancePlayer ||
                                     EnmityContainer.AllegianceOf(master) == CharData.AllegiancePlayer;
                }
                // do not include pets in monster AoE buffs
                if (!monsterPlayer_ && target_ != null && target_.PMaster == null) withPet = false;
                if (selfCentered_) Add(caster_);
                if (monsterPlayer_ && (mob.GetMobMod(MobMod.AoeHitAll) != 0 || mob.CallForHelp))
                {
                    AddAllInZone(withPet);
                    return;
                }
                // addAllInAlliance
                if (master is CharData mc) foreach (var member in host_.ForAlliance(mc)) Add(member, withPet);
                else Add(master, withPet);
                if (!monsterPlayer_) return;
                if (mob.Allegiance == CharData.AllegiancePlayer || (master is MobEntity mm and not PetEntity && mm.Allegiance == CharData.AllegiancePlayer)) AddAllInZone(withPet);
                else foreach (var e in new List<EnmityContainer.Entry>(mob.Enmity.Entries)) if (e.Owner != null) Add(e.Owner);
            }

            public void WithinCone(BattleEntity t, float distance, float angle, ushort flags, byte aoeType, byte findFlags = 0)
            {
                flags_ = flags;
                conal_ = true;
                (ax_, az_) = (caster_.X, caster_.Z);
                byte halfAngle = (byte)(angle * (256.0f / 360.0f) / 2.0f);
                byte toTarget = RelativeAngle(caster_.Rotation, aoeType == AoeConalRear ? 128 : 0);
                float left = RotationToRadian(RelativeAngle(toTarget, -halfAngle));
                float right = RotationToRadian(RelativeAngle(toTarget, halfAngle));
                bx_ = MathF.Cos(2 * MathF.PI - right) * distance;
                bz_ = MathF.Sin(2 * MathF.PI - right) * distance;
                ccx_ = MathF.Cos(2 * MathF.PI - left) * distance;
                ccz_ = MathF.Sin(2 * MathF.PI - left) * distance;
                scalar_ = bx_ * ccz_ - bz_ * ccx_;
                WithinArea(t, AoeAttacker, distance, flags, findFlags);
            }

            private static byte RelativeAngle(byte world, int diff)
            {
                int a = (short)(world + diff);
                if (a < 0) a = 256 - Math.Abs(a);
                return (byte)a;
            }

            private static float RotationToRadian(byte rotation) => rotation / 256.0f * 2 * MathF.PI;

            private void AddAllInZone(bool withPet)
            {
                foreach (var c in new List<CharData>(host_.charSessions_.Keys))
                    if (SameArea(c, caster_)) Add(c, withPet);
                foreach (var mob in host_.AreaOf(caster_).Mobs) Add(mob, withPet);
                foreach (var trust in host_.AreaOf(caster_).Trusts) Add(trust, withPet);
            }

            // CTargetFind::addEntity: the entity if valid, and its pet too when allowed
            public void Add(BattleEntity t, bool withPet = false)
            {
                if (Valid(t)) Targets.Add(t);
                if (withPet && t?.PPet != null && Valid(t.PPet)) Targets.Add(t.PPet);
            }

            // CTargetFind::validEntity
            private bool Valid(BattleEntity t)
            {
                if (t == null || Targets.Contains(t)) return false;
                bool dead = t is MobEntity dm ? dm.IsDead || !dm.IsAlive : t.Hp <= 0;
                if (dead && (findFlags_ & FindFlagsDead) == 0) return false;
                if ((findFlags_ & FindFlagsIgnoreBattleId) == 0 && caster_.BattleId != t.BattleId) return false;
                if (t is MobEntity { IsSpawned: false } || IsUntargetable(t)) return false;
                if (t == target_ || !SameArea(t, caster_)) return false;
                // m_Locked chars take no ability or spell, AoEs included
                if (t is CharData c && (host_.SessionOf(c) == null || c.Locked)) return false;
                int ta = EnmityContainer.AllegianceOf(t);
                // the caster's master's allegiance (findMaster)
                if (selfCentered_ && (flags_ & (TargetAnyAllegiance | TargetEnemy)) == 0 &&
                    EnmityContainer.AllegianceOf(caster_.PMaster ?? caster_) != ta) return false;
                // Retail caps at 8.5y for mob self-centered AoE, 8y for everything else (a pet included).
                if (MathF.Abs(t.Y - cy_) >= (selfCentered_ && caster_ is MobEntity and not PetEntity ? 8.5f : 8.0f)) return false;
                if (target_ == null && (!selfCentered_ || conal_)) return true;
                if ((flags_ & TargetAnyAllegiance) != 0)
                {
                    if (t == caster_) return false;
                }
                else
                {
                    if (target_ != null && EnmityContainer.AllegianceOf(target_) != ta) return false;
                    if ((flags_ & TargetEnemy) != 0 && (flags_ & TargetPlayerParty) == 0 && CasterAllegiance == ta) return false;
                }
                return conal_ ? WithinCone(t) : WithinRadius(t);
            }

            private bool WithinRadius(BattleEntity t)
            {
                float dx = t.X - cx_, dy = t.Y - cy_, dz = t.Z - cz_;
                return MathF.Sqrt(dx * dx + dy * dy + dz * dz) <= radius_;
            }

            private bool WithinCone(BattleEntity t)
            {
                float px = t.X - ax_, pz = t.Z - az_;
                float w0 = (px * (bz_ - ccz_) + pz * (ccx_ - bx_) + bx_ * ccz_ - ccx_ * bz_) / scalar_;
                float w1 = (px * ccz_ - pz * ccx_) / scalar_;
                float w2 = (pz * bx_ - px * bz_) / scalar_;
                return w0 >= 0 && w0 <= 1 && w1 >= 0 && w1 <= 1 && w2 >= 0 && w2 <= 1;
            }
        }

        // ---- the Lua hooks ------------------------------------------------------------------------------------------

        // luautils::OnMobMobskillChoose: the mob script's pick, if a number above 0
        private int OnMobMobskillChoose(MobEntity m, BattleEntity target, ushort chosen)
        {
            if (lua_ == null) return 0;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(MobScriptFile(m), "onMobMobskillChoose");
                if (lua_.IsNil(-1)) return 0;
                lua_.Push(m);
                lua_.Push(target);
                lua_.Push((int)chosen);
                lua_.Call(3, 1);
                int v = lua_.IsNumber(-1) ? (ushort)lua_.ToIntegral(-1) : 0;
                return v > 0 ? v : 0;
            }
            catch (LuaException e)
            {
                log_($"luautils::onMobMobskillChoose: {e.Message}");
                return 0;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnMobSkillTarget: the mob script may swap the target; nil keeps it
        private BattleEntity OnMobSkillTarget(BattleEntity target, MobEntity m, MobSkill skill)
        {
            if (lua_ == null) return target;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(MobScriptFile(m), "onMobSkillTarget");
                if (lua_.IsNil(-1)) return target;
                lua_.Push(target);
                lua_.Push(m);
                lua_.Push(skill);
                lua_.Call(3, 1);
                return lua_.ToObject(-1) as BattleEntity ?? target;
            }
            catch (LuaException e)
            {
                log_($"luautils::onMobSkillTarget: {e.Message}");
                return target;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnMobSkillCheck: 0 lets the skill go; no script or an error 1, a non-number -5
        private int OnMobSkillCheck(BattleEntity target, MobEntity m, MobSkill skill)
        {
            if (lua_ == null) return 1;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(MobSkillScript(skill), "onMobSkillCheck");
                if (lua_.IsNil(-1)) return 1;
                lua_.Push(target);
                lua_.Push(m);
                lua_.Push(skill);
                lua_.Call(3, 1);
                return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : -5;
            }
            catch (LuaException e)
            {
                log_($"luautils::onMobSkillCheck: {e.Message}");
                return 1;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnMobSkillReadyTime: the mob script's ready time in ms, if a number
        private long? OnMobSkillReadyTime(BattleEntity target, MobEntity m, MobSkill skill)
        {
            if (lua_ == null) return null;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(MobScriptFile(m), "onMobSkillReadyTime");
                if (lua_.IsNil(-1)) return null;
                lua_.Push(target);
                lua_.Push(m);
                lua_.Push(skill);
                lua_.Call(3, 1);
                return lua_.IsNumber(-1) ? (ushort)lua_.ToIntegral(-1) : null;
            }
            catch (LuaException e)
            {
                log_($"luautils::onMobSkillReadyTime: {e.Message}");
                return null;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnMobWeaponSkill: the mob script's onMobWeaponSkill (its result unused), then the skill's, whose
        // number is the damage
        private int OnMobWeaponSkill(BattleEntity m, BattleEntity target, MobSkill skill, ActionPacket action)
        {
            if (lua_ == null) return 0;
            int top = lua_.Top;
            if (m is MobEntity scripted)
            try
            {
                scripts_.PushFileFunction(MobScriptFile(scripted), "onMobWeaponSkill");
                if (!lua_.IsNil(-1))
                {
                    lua_.Push(m);
                    lua_.Push(target);
                    lua_.Push(skill);
                    lua_.Push(action);
                    lua_.Call(4, 0);
                }
            }
            catch (LuaException e)
            {
                log_($"luautils::onMobWeaponSkill (mob) {e.Message}");
                return 0;
            }
            finally
            {
                lua_.SetTop(top);
            }
            try
            {
                scripts_.PushFileFunction(MobSkillScript(skill), "onMobWeaponSkill");
                if (lua_.IsNil(-1)) return 0;
                lua_.Push(m);
                lua_.Push(target);
                lua_.Push(skill);
                lua_.Push(action);
                lua_.Call(4, 1);
                return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : 0;
            }
            catch (LuaException e)
            {
                log_($"luautils::onMobWeaponSkill (mobskill) {e.Message}");
                return 0;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnMobSkillFinalize: once per skill that landed, targets or not
        private void OnMobSkillFinalize(BattleEntity m, MobSkill skill)
        {
            if (lua_ == null) return;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(MobSkillScript(skill), "onMobSkillFinalize");
                if (lua_.IsNil(-1)) return;
                lua_.Push(m);
                lua_.Push(skill);
                lua_.Call(2, 0);
            }
            catch (LuaException e)
            {
                log_($"luautils::onMobSkillFinalize: {e.Message}");
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::callGlobal<Knockback>("xi.combat.knockback.calculate", target, actor, skill or spell, action)
        private int CalculateKnockback(BattleEntity target, BattleEntity m, object skill, ActionPacket action)
        {
            const string path = "xi.combat.knockback.calculate";
            if (lua_ == null) return 0;
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (lua_.IsNil(-1))
                {
                    log_($"luautils::callGlobalFunction: {path}: Function not found");
                    return 0;
                }
                lua_.Push(target);
                lua_.Push(m);
                lua_.Push(skill);
                lua_.Push(action);
                lua_.Call(4, 1);
                return lua_.IsNumber(-1) ? (int)lua_.ToEnum(-1) : 0; // callGlobal<Knockback>: an enum
            }
            catch (LuaException e)
            {
                log_($"luautils::callGlobalFunction: {path}: {e.Message}");
                return 0;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        private static string MobSkillScript(MobSkill skill) => $"./scripts/actions/mobskills/{skill.Name}.lua";

        // ---- bindings -----------------------------------------------------------------------------------------------

        private void RegisterMobSkillBindings(Dictionary<string, LuaFunction> m)
        {
            m["useMobAbility"] = UseMobAbility;
            m["useJobAbility"] = UseJobAbility;

            static MobSkill Skill(LuaState l) =>
                l.ToObject(1) as MobSkill ?? throw new InvalidOperationException("bad self: expected " + MobSkillType);
            // CLuaPetSkill: the pet skill's methods
            static MobSkill PetSkill(LuaState l) =>
                l.ToObject(1) as MobSkill ?? throw new InvalidOperationException("bad self: expected " + PetSkillType);
            lua_.RegisterType(PetSkillType, new Dictionary<string, LuaFunction>
            {
                ["setMsg"] = l =>
                {
                    PetSkill(l).Msg = (ushort)l.ToEnumArg(2); // MsgBasic
                    return 0;
                },
                ["getMsg"] = l => Ret(l, PetSkill(l).Msg),
                ["hasMissMsg"] = l => Ret(l, PetSkill(l).HasMissMsg),
                ["isAoE"] = l => Ret(l, PetSkill(l).IsAoE),
                ["isConal"] = l => Ret(l, PetSkill(l).IsConal),
                ["isSingle"] = l => Ret(l, PetSkill(l).IsSingle),
                ["getParam"] = l => Ret(l, PetSkill(l).Param),
                ["getID"] = l => Ret(l, PetSkill(l).Id),
                ["getTotalTargets"] = l => Ret(l, PetSkill(l).TotalTargets),
                ["getPrimaryTargetID"] = l => Ret(l, PetSkill(l).PrimaryTargetId),
                ["setFinalAnimationSub"] = l =>
                {
                    PetSkill(l).FinalAnimationSub = (byte)l.ToIntArg(2);
                    return 0;
                },
                ["getTP"] = l => Ret(l, (float)PetSkill(l).Tp),
                ["getMobHP"] = l => Ret(l, PetSkill(l).Hp),
                ["getMobHPP"] = l => Ret(l, PetSkill(l).Hpp),
                ["getAttackType"] = l => Ret(l, PetSkill(l).AttackType),
                ["setAttackType"] = l =>
                {
                    PetSkill(l).AttackType = (byte)l.ToEnumArg(2); // xi::AttackType
                    return 0;
                },
                ["isCritical"] = l => Ret(l, PetSkill(l).Critical),
                ["setCritical"] = l =>
                {
                    PetSkill(l).Critical = l.ToBoolean(2);
                    return 0;
                },
            });
            lua_.RegisterType(MobSkillType, new Dictionary<string, LuaFunction>
            {
                ["setMsg"] = l =>
                {
                    Skill(l).Msg = (ushort)l.ToEnumArg(2); // MsgBasic
                    return 0;
                },
                ["getMsg"] = l => Ret(l, Skill(l).Msg),
                ["hasMissMsg"] = l => Ret(l, Skill(l).HasMissMsg),
                ["isAoE"] = l => Ret(l, Skill(l).IsAoE),
                ["isConal"] = l => Ret(l, Skill(l).IsConal),
                ["isSingle"] = l => Ret(l, Skill(l).IsSingle),
                ["getParam"] = l => Ret(l, Skill(l).Param),
                ["getID"] = l => Ret(l, Skill(l).Id),
                ["getTargets"] = l =>
                {
                    var targets = Skill(l).Targets;
                    l.NewTable();
                    for (int i = 0; i < targets.Count; i++)
                    {
                        l.PushObject(targets[i]);
                        l.RawSetI(-2, i + 1);
                    }
                    return 1;
                },
                ["getTotalTargets"] = l => Ret(l, Skill(l).TotalTargets),
                ["getPrimaryTargetID"] = l => Ret(l, Skill(l).PrimaryTargetId),
                ["setFinalAnimationSub"] = l =>
                {
                    Skill(l).FinalAnimationSub = (byte)l.ToIntArg(2);
                    return 0;
                },
                ["setAnimationTime"] = l =>
                {
                    Skill(l).AnimationTimeMs = l.ToUIntArg(2);
                    return 0;
                },
                ["getTP"] = l => Ret(l, (float)Skill(l).Tp),
                ["getMobHP"] = l => Ret(l, Skill(l).Hp),
                ["getMobHPP"] = l => Ret(l, Skill(l).Hpp),
                ["getAttackType"] = l => Ret(l, Skill(l).AttackType),
                ["setAttackType"] = l =>
                {
                    Skill(l).AttackType = (byte)l.ToEnumArg(2); // xi::AttackType
                    return 0;
                },
                ["isCritical"] = l => Ret(l, Skill(l).Critical),
                ["setCritical"] = l =>
                {
                    Skill(l).Critical = l.ToBoolean(2);
                    return 0;
                },
                ["getKnockback"] = l => Ret(l, Skill(l).Knockback),
            });
        }

        // CLuaBaseEntity::useMobAbility(skillId, target, castMs, ignoreDistance): queued until the AI can change
        // state; with no arguments the controller picks from the mob's list
        private int UseMobAbility(LuaState l)
        {
            if (Entity(l) is not MobEntity m)
            {
                log_($"Entity is not a Trust, Mob, or Pet ({NameOf(Entity(l))}).");
                return 0;
            }
            if (l.Top < 2)
            {
                QueueNative(m, 0, true, e => MobSkillFromList((MobEntity)e, 0));
                return 0;
            }
            ushort id = (ushort)l.ToIntArg(2);
            var skill = GetMobSkill(id);
            if (skill == null) return 0;
            var target = l.Top >= 3 ? l.ToObject(3) as BattleEntity : null;
            long? castMs = l.Top >= 4 && l.IsNumber(4) ? (long)(uint)l.ToIntegral(4) : null;
            bool ignoreDistance = l.Top >= 5 && l.Type(5) == LuaNative.LUA_TBOOLEAN && l.ToBoolean(5);
            QueueNative(m, 0, true, e =>
            {
                var mob = (MobEntity)e;
                long now = NowMs();
                if (target != null && EntityExists(target))
                {
                    if (ignoreDistance || Distance(mob, target) <= skill.Distance) MobUseSkill(mob, target, id, castMs, now);
                }
                else if (skill.Aoe == AoeAttacker)
                {
                    MobUseSkill(mob, mob, id, castMs, now);
                }
                else if ((skill.ValidTargets & TargetEnemy) != 0)
                {
                    if (mob.BattleTarget is { } battleTarget && (ignoreDistance || Distance(mob, battleTarget) <= skill.Distance))
                        MobUseSkill(mob, battleTarget, id, castMs, now);
                }
                else if ((skill.ValidTargets & TargetSelf) != 0)
                {
                    MobUseSkill(mob, mob, id, castMs, now);
                }
            });
            return 0;
        }

        // CLuaBaseEntity::useJobAbility(abilityId, target): queued (0 ms, until the AI can change state), the ability on
        // the target while it still exists, else a mob's on its battle target
        private int UseJobAbility(LuaState l)
        {
            var self = Entity(l);
            ushort id = (ushort)l.ToIntArg(2);
            var target = l.Top >= 3 ? l.ToObject(3) as BattleEntity : null;
            QueueNative(self, 0, true, e =>
            {
                var t = target != null && EntityExists(target) ? target : null;
                if (t == null && e is MobEntity owner) t = owner.BattleTarget;
                if (t == null) return;
                if (e is MobEntity mob) MobAbility(mob, t, id, NowMs());
                // a char: CPlayerController::Ability on the target's act index
                else if (e is CharData c && SessionOf(c) is { } s) AbilityUse(s, c, t.TargId, id);
            });
            return 0;
        }

        // CLuaAction's methods past the ones an item use needs
        private static void AddActionBindings(Dictionary<string, LuaFunction> a)
        {
            static ActionPacket Self(LuaState l) =>
                l.ToObject(1) as ActionPacket ?? throw new InvalidOperationException("bad self: expected " + ActionType);
            static ActionResult Result(LuaState l, uint id)
            {
                foreach (var t in Self(l).Targets)
                    if (t.ActorId == id && t.Results.Count != 0) return t.Results[0];
                return null;
            }
            static int Set(LuaState l, Action<ActionResult> set)
            {
                if (Result(l, l.ToUIntArg(2)) is ActionResult r) set(r);
                return 0;
            }
            a["getPrimaryTargetID"] = l => Ret(l, Self(l).Targets.Count != 0 ? Self(l).Targets[0].ActorId : 0u);
            a["getRecast"] = l => Ret(l, Self(l).RecastSec);
            a["setRecast"] = l =>
            {
                Self(l).RecastSec = (ushort)l.ToIntArg(2);
                return 0;
            };
            a["getMsg"] = l =>
            {
                if (Result(l, l.ToUIntArg(2)) is not ActionResult r)
                {
                    l.PushNil();
                    return 1;
                }
                return Ret(l, r.MessageId);
            };
            a["getAnimation"] = l =>
            {
                if (Result(l, l.ToUIntArg(2)) is not ActionResult r)
                {
                    l.PushNil();
                    return 1;
                }
                return Ret(l, r.Animation);
            };
            a["setAnimation"] = l => Set(l, r => r.Animation = (ushort)l.ToEnumArg(3));
            a["getCategory"] = l => Ret(l, Self(l).ActionType);
            a["setCategory"] = l =>
            {
                Self(l).ActionType = (byte)l.ToIntArg(2);
                return 0;
            };
            // the action's enum arguments (ActionAnimation, ActionResolution, ...): LuaState.ToEnumArg
            a["resolution"] = l => Set(l, r => r.Resolution = (byte)l.ToEnumArg(3));
            a["info"] = l => Set(l, r => r.Info |= (byte)l.ToEnumArg(3));
            a["hitDistortion"] = l => Set(l, r => r.HitDistortion = (byte)l.ToEnumArg(3));
            a["knockback"] = l => Set(l, r => r.Knockback = (byte)l.ToEnumArg(3));
            a["modifier"] = l => Set(l, r => r.Modifier = (byte)l.ToIntArg(3));
            a["recordDamage"] = l =>
            {
                if (l.ToObject(2) is BattleEntity t && Result(l, t.Id) is ActionResult r)
                    RecordDamage(r, (byte)l.ToEnumArg(3), l.ToIntArg(4), t, !l.IsNil(5) && l.ToBoolean(5));
                return 0;
            };
            // the additional effect fields are not in the C# action: these are kept as no-ops
            a["additionalEffect"] = _ => 0;
            a["addEffectParam"] = _ => 0;
            a["addEffectMessage"] = _ => 0;
            a["addAdditionalTarget"] = l =>
            {
                uint id = l.ToUIntArg(2);
                foreach (var t in Self(l).Targets)
                    if (t.ActorId == id) return Ret(l, false);
                Self(l).AddTarget(id).AddResult();
                return Ret(l, true);
            };
        }
    }
}
