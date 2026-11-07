using System;
using System.Collections.Generic;
using XI.Map;
using XI.Script;

namespace XI.Zone
{
    // Player pets, batch 3: a char's pet commands and its pet's skills (blood pacts, breaths, ready moves).
    // charutils::BuildingCharPetAbilityTable / hasPetAbility, the ability state's PetSkillDistanceCheck and the
    // isPetAbility branch of CCharEntity::OnAbility, battleutils::LoadPetSkillsList, CPetController::PetSkill,
    // CPetSkillState and CPetEntity::OnPetSkillFinished with luautils::OnPetAbility. A pet skill is a MobSkill with
    // IsPetSkill set; it runs in the pet's skill state (MobSkillState), whose update and cleanup branch on it.
    // (An automaton's moves are mob skills with OnAutomatonAbility, ZoneHost.AutomatonAi.cs.) Not ported: the pet's own job
    // abilities (CPetEntity::OnAbility).
    public sealed partial class ZoneHost
    {
        private const string PetSkillType = "CPetSkill";
        private const byte FindFlagsPet = 0x04; // FINDFLAGS_PET
        private const byte FindFlagsIgnoreBattleId = 0x20; // FINDFLAGS_IGNORE_BATTLEID
        private const ushort TargetIgnoreBattleId = 0x0400; // TARGET_IGNORE_BATTLEID: hits targets of another battle id
        private const ushort AbilitySoothingRuby = 520, AbilityMoonlitCharge = 528, AbilityCamisado = 656, AbilityBlindside = 667,
            AbilityDeconstruction = 668, AbilityPacifyingRuby = 773, AbilityClarsachCall = 960, AbilityHystericAssault = 970;
        private const ushort MsgReadiesSkill = 326, MsgTooFarAwayRed = 328;
        private const ushort AnimPetSkillStart = 94;

        private Dictionary<ushort, MobSkill> petSkills_;

        // battleutils::LoadPetSkillsList
        private void LoadPetSkills()
        {
            if (petSkills_ != null) return;
            petSkills_ = new Dictionary<ushort, MobSkill>();
            var r = Exec("SELECT pet_skill_id, pet_anim_id, pet_skill_name, pet_skill_aoe, pet_skill_radius, pet_skill_distance, " +
                         "pet_anim_time, pet_prepare_time, pet_valid_targets, pet_message, pet_skill_flag, pet_skill_param, " +
                         "pet_skill_finish_category, knockback, primary_sc, secondary_sc, tertiary_sc, mob_skill_id FROM pet_skills");
            while (r != null && r.Next())
            {
                var s = new MobSkill
                {
                    IsPetSkill = true,
                    Id = r.Get<ushort>("pet_skill_id"),
                    AnimationId = r.Get<ushort>("pet_anim_id"),
                    Name = r.Get<string>("pet_skill_name"),
                    Aoe = r.Get<byte>("pet_skill_aoe"),
                    AoeRadius = r.Get<byte>("pet_skill_radius"),
                    Distance = r.Get<float>("pet_skill_distance"),
                    AnimationTimeMs = r.Get<uint>("pet_anim_time"),
                    ActivationTimeMs = r.Get<uint>("pet_prepare_time"),
                    ValidTargets = r.Get<ushort>("pet_valid_targets"),
                    Msg = r.Get<ushort>("pet_message"),
                    Flag = r.Get<byte>("pet_skill_flag"),
                    Param = r.Get<short>("pet_skill_param"),
                    FinishCategory = r.Get<byte>("pet_skill_finish_category"),
                    Knockback = r.Get<byte>("knockback"),
                    PrimarySc = r.Get<byte>("primary_sc"),
                    SecondarySc = r.Get<byte>("secondary_sc"),
                    TertiarySc = r.Get<byte>("tertiary_sc"),
                    MobSkillId = r.Get<ushort>("mob_skill_id"),
                };
                petSkills_[s.Id] = s;
            }
        }

        // battleutils::GetPetSkill, with the skill's script loaded on first use
        private MobSkill GetPetSkill(int id)
        {
            LoadPetSkills();
            if (id < 0 || id > ushort.MaxValue || !petSkills_.TryGetValue((ushort)id, out var s)) return null;
            AbilityLoadScript(PetSkillScript(s));
            // a jug pet's move runs its mob skill's script (xi.actions.mobskills[name]), which C++ loads with every
            // mob skill at startup
            if (s.MobSkillId > 0) GetMobSkill(s.MobSkillId);
            return s;
        }

        private static string PetSkillScript(MobSkill skill) => $"./scripts/actions/abilities/pets/{skill.Name}.lua";

        // ---- the master's pet commands ------------------------------------------------------------------------------

        // charutils::hasPetAbility / addPetAbility: bits of m_PetCommands
        private static bool HasPetAbility(CharData c, int bit) => bit >= 0 && bit / 8 < c.PetCommands.Length && (c.PetCommands[bit / 8] & 1 << (bit % 8)) != 0;

        private static void AddPetAbility(CharData c, int bit)
        {
            if (bit >= 0 && bit / 8 < c.PetCommands.Length) c.PetCommands[bit / 8] |= (byte)(1 << (bit % 8));
        }

        // charutils::BuildingCharPetAbilityTable: a SMN's blood pacts for the avatar out (each at the avatar's level and
        // past CheckAbilityAddtype), a jug pet's moves from its skill list when the master has no Sic; pet 0 blanks them
        private void BuildPetAbilityTable(CharData c, PetEntity pet, int petId)
        {
            Array.Clear(c.PetCommands);
            if (petId == 0)
            {
                // technically Fire Spirit but we're using this to null the abilities shown
                PushTo(c, LoginPackets.CommandData(c));
                return;
            }
            const int healingRuby = AbilityInfo.HealingRuby;
            if ((c.MJob == MobStats.JobSmn || c.SJob == MobStats.JobSmn) && loader_.Abilities is { } table)
            {
                foreach (var a in table.ForJob(MobStats.JobSmn))
                {
                    if (pet.MLvl < a.Level || !PetId.IsAvatar(petId) || !CharLoader.AddTypeOk(c, a)) continue;
                    if (petId == PetId.Carbuncle)
                    {
                        if (a.Id >= healingRuby && a.Id <= AbilitySoothingRuby) AddPetAbility(c, a.Id - healingRuby);
                        else if (a.Id == AbilityPacifyingRuby) AddPetAbility(c, 261);
                    }
                    else if (petId >= PetIdFenrir && petId <= PetIdRamuh)
                    {
                        if (a.Id >= healingRuby + (petId - 8) * 16 && a.Id < healingRuby + (petId - 7) * 16) AddPetAbility(c, a.Id - healingRuby);
                    }
                    else if (petId == PetIdDiabolos)
                    {
                        if (a.Id >= AbilityCamisado && a.Id <= AbilityBlindside) AddPetAbility(c, a.Id - healingRuby);
                    }
                    else if (petId == PetId.CaitSith)
                    {
                        if (a.Id > AbilitySoothingRuby && a.Id < AbilityMoonlitCharge) AddPetAbility(c, a.Id - healingRuby);
                    }
                    else if (petId == PetId.Siren)
                    {
                        const int sirenAbilityPacketOffset = 0x1C0;
                        if (a.Id >= AbilityClarsachCall && a.Id <= AbilityHystericAssault) AddPetAbility(c, a.Id - AbilityClarsachCall + sirenAbilityPacketOffset);
                    }
                }
            }
            if (pet.PetType == PetType.JugPet && !AbilityLearned(c, AbilityInfo.Sic))
                foreach (var id in GetMobSkillList(pet.MobSkillList)) AddPetAbility(c, id - healingRuby);
            PushTo(c, LoginPackets.CommandData(c));
        }

        private const int PetIdFenrir = 9, PetIdRamuh = 15, PetIdDiabolos = 16;

        // PetSkillDistanceCheck: a blood pact needs the summoner within 20 yalms (and hitboxes) of the target and the
        // avatar within the skill's range; a ready move needs the master within 4 yalms of the pet and the pet within
        // the skill's range of its foe
        private bool PetSkillDistanceCheck(MapSession s, CharData c, BattleEntity target, AbilityInfo ability)
        {
            var skill = GetPetSkill(ability.Id);
            if (c.PPet is not PetEntity pet || skill == null) return false;
            if (skill.IsBloodPactRage || skill.IsBloodPactWard)
            {
                if (c != target && Distance(c, target) > 20.0f + HitboxSize(c) + HitboxSize(target)) return false;
                if (Distance(pet, target) > skill.Distance + HitboxSize(pet) + HitboxSize(target))
                {
                    Push(s, SelfPackets.BattleMessage(c.Id, c.TargId, target.Id, target.TargId, 0, 0, MsgTargetOutOfRange));
                    return false;
                }
            }
            else if (skill.MobSkillId > 0)
            {
                if (Distance(c, pet) > 4.0f + HitboxSize(c) + HitboxSize(pet))
                {
                    Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgTargetOutOfRange));
                    return false;
                }
                if ((skill.ValidTargets & TargetEnemy) != 0 && pet.BattleTarget is { } petTarget &&
                    Distance(pet, petTarget) > skill.Distance + HitboxSize(pet) + HitboxSize(petTarget))
                {
                    Push(s, SelfPackets.BattleMessage(c.Id, c.TargId, petTarget.Id, petTarget.TargId, 0, 0, MsgTargetOutOfRange));
                    return false;
                }
            }
            return true;
        }

        // CCharEntity::OnAbility's pet ability: the job ability packet (on the master for a jug pet's move), then the
        // pet is told to use its skill (a jug pet picks its own target: its foe, or itself)
        private void CharPetAbility(CharData c, BattleEntity target, AbilityInfo ability, ActionPacket action)
        {
            var skill = GetPetSkill(ability.Id);
            // a real pet (a charmed mob is none) with the ability in pet_skills
            if (c.PPet is not PetEntity pet || skill == null) return;
            var r = action.AddTarget(pet.PetType == PetType.JugPet ? c.Id : target.Id).AddResult();
            r.Animation = AnimPetSkillStart;
            r.Resolution = 0; // ActionResolution::Hit
            BattleEntity petTarget = target;
            if (pet.PetType == PetType.JugPet) petTarget = (skill.ValidTargets & TargetEnemy) != 0 ? pet.BattleTarget : pet;
            PetUseSkill(pet, petTarget, skill.Id, NowMs());
        }

        // ---- the pet's skill state ----------------------------------------------------------------------------------

        // CPetController::PetSkill and CAIContainer::Internal_PetSkill: face it, WEAPONSKILL_BEFORE_USE, then the state
        private bool PetUseSkill(PetEntity pet, BattleEntity target, ushort id, long now)
        {
            if (target != null) FaceTarget(pet, target);
            TriggerListener(pet, "WEAPONSKILL_BEFORE_USE", pet, (int)id);
            if (target != null && IsUntargetable(target)) return false;
            if (!CanChangeState(pet)) return false;
            return StartPetSkill(pet, target, id, now);
        }

        // CPetSkillState::init: the readies packet when the skill takes time (shown on the pet under
        // HIDE_READIES_TARGET; a wyvern follows it with an interrupt unless WYVERN_SHOW_READYING), then the TP spent
        private bool StartPetSkill(PetEntity pet, BattleEntity target, ushort id, long now)
        {
            var template = GetPetSkill(id);
            if (template == null) return false;
            if (pet.StatusEffects.HasStatusEffect(EffectAmnesia, EffectImpairment)) return false;
            if (target == null || !EntityExists(target) || !ValidSkillTarget(target, pet, template.ValidTargets)) return false;

            var st = new MobSkillState { Skill = template.Clone(), Target = target, EntryMs = now };
            st.CastMs = st.Skill.ActivationTimeMs;
            st.SwingRemainingMs = Math.Max(0, pet.SwingAtMs - now);
            pet.SkillState = st;
            if (st.CastMs > 0)
            {
                uint shownId = ChatCfg.HideReadiesTarget ? pet.Id : target.Id;
                bool asMobSkill = st.Skill.MobSkillId > 0;
                var readies = new ActionPacket { ActorId = pet.Id, ActionType = ActionCategory.SkillStart, ActionId = (uint)FourCC.SkillUse };
                var r = readies.AddTarget(shownId).AddResult();
                r.Param = asMobSkill ? st.Skill.MobSkillId : st.Skill.Id;
                r.MessageId = asMobSkill ? MsgSkillReadies : MsgReadiesSkill;
                PushMobInRange(pet, Battle2Packets.Battle2(readies));
                // Wyverns immediately emit a skill interrupt packet (retail accurate)
                if (pet.PetId == PetId.Wyvern && pet.GetMod(Mod.WYVERN_SHOW_READYING) == 0) AbilityInterrupt(pet);
            }
            TriggerListener(pet, "WEAPONSKILL_STATE_ENTER", pet, (int)st.Skill.Id);
            // CPetSkillState::SpendCost
            if (!st.Skill.IsTpFree)
            {
                st.SpentTp = pet.Tp;
                pet.Tp = 0;
            }
            return true;
        }

        // the end of CPetSkillState::Update once the animation is over: a landed skill on a foe gives it base enmity
        // toward the pet and its master; a blood pact raises Avatar's Favor's power and sends the avatar (not
        // Alexander or Atomos) back at a live foe
        private void PetSkillDone(PetEntity pet, MobSkillState st, long now)
        {
            var target = st.Target != null && EntityExists(st.Target) ? st.Target : null;
            if (st.Success && target is MobEntity mob && mob != pet && pet.Allegiance != mob.Allegiance)
                mob.Enmity.UpdateEnmity(pet, 0, 0, withMaster: true);

            if (pet.PMaster is not CharData summoner || !(st.Skill.IsBloodPactRage || st.Skill.IsBloodPactWard)) return;
            if (summoner.StatusEffects.GetStatusEffect(EffectAvatarsFavor) is { } favor)
            {
                // Retail: Power is gained for BP use
                int power = favor.Power + (st.Skill.IsBloodPactRage ? 3 : 2);
                favor.Power = (ushort)Math.Max(power, 11);
            }
            if (target is MobEntity foe && pet.PetType == PetType.Avatar && pet.PetId != PetId.Alexander && pet.PetId != PetId.Atomos &&
                foe.IsAlive && !foe.IsDead && foe.Allegiance != pet.Allegiance)
            {
                // Re-engage the target after blood pact
                PetEngage(pet, foe, now);
            }
        }

        // CPetEntity::OnPetSkillFinished: the targets around the target (the pet itself first for a self AoE), the
        // skill's script on each, the messages, damage, knockback and skillchain, then the claims and the effect flags.
        // Out of range, an avatar or a wyvern shows the interrupt instead.
        private void OnPetSkillFinished(PetEntity pet, MobSkillState st, ActionPacket action, long now)
        {
            var skill = st.Skill;
            BattleEntity target = st.Target;
            if (target == null || !EntityExists(target))
            {
                log_("CMobEntity::OnMobSkillFinished: PTarget is null");
                return;
            }
            var find = new SkillTargetFind(this, pet);
            byte findFlags = 0;
            // Mob buff abilities also hit monster's pets
            if ((skill.ValidTargets & TargetSelf) != 0)
            {
                findFlags |= FindFlagsPet;
                // skill can target self and is aoe, add itself to targetfind first
                if (skill.IsAoE) target = pet;
            }
            if ((skill.ValidTargets & TargetIgnoreBattleId) == TargetIgnoreBattleId) findFlags |= FindFlagsIgnoreBattleId;
            if ((skill.ValidTargets & TargetPlayerDead) == TargetPlayerDead) findFlags |= FindFlagsDead;

            action.ActorId = pet.Id;
            action.ActionType = skill.FinishCategory;
            // jug pet skills emulate mob skills but still have the same flow as wyvern and smn pet skills
            action.ActionId = skill.MobSkillId > 0 ? skill.MobSkillId : skill.Id;

            if (Distance(pet, target) <= skill.Distance + pet.ModelHitboxSize + HitboxSize(target))
            {
                if (skill.IsAoE) find.WithinArea(target, skill.Aoe, skill.Radius, skill.ValidTargets, findFlags);
                else if (skill.IsConal) find.WithinCone(target, skill.Radius, 45f, skill.ValidTargets, 0, findFlags);
                else
                {
                    find.SingleTarget(target, skill.ValidTargets, findFlags);
                    // special jug pet skills that affect pet and owner: the primary target is the pet and the skill
                    // takes the actor's party (addEntity does not handle range checking)
                    if (pet.PetType == PetType.JugPet && pet.PMaster is { } master && target == pet && (skill.ValidTargets & TargetPlayerParty) != 0 &&
                        Distance(pet, master) <= skill.Radius)
                        find.Add(master);
                }
            }
            else
            {
                if (pet.PetType == PetType.Avatar || pet.PetType == PetType.Wyvern) PetSkillOutOfRange(pet, skill, target);
                return;
            }

            // No targets, perhaps something like Super Jump or otherwise untargetable
            if (find.Targets.Count == 0) return;

            skill.Targets = new List<BattleEntity>(find.Targets);
            skill.TotalTargets = (ushort)find.Targets.Count;
            skill.PrimaryTargetId = target.Id;
            skill.Tp = st.SpentTp;
            skill.Hp = pet.Hp;
            skill.Hpp = (byte)pet.Hpp;

            ushort msg = 0, defaultMessage = skill.Msg;
            bool first = true;
            foreach (var t in find.Targets)
            {
                var r = action.AddTarget(t.Id).AddResult();
                r.Resolution = 0;
                r.Animation = skill.AnimationId;
                r.MessageId = skill.Msg;
                // reset the skill's message back to default
                skill.Msg = defaultMessage;
                int damage = OnPetAbility(t, pet, skill, pet.PMaster, action);

                // primary target will have msg == 0
                msg = msg == 0 ? skill.Msg : AoEVariant(skill.Msg);
                if (damage < 0)
                {
                    // damage was absorbed
                    msg = MsgSkillRecoversHp;
                    RecordDamage(r, 1, Math.Clamp(-damage, 0, Math.Max(0, t.MaxHp - t.Hp)), t, false);
                }
                else if (damage > 0 && skill.IsDamageMsg)
                {
                    // We use the skill to carry the critical flag and the attack type
                    RecordDamage(r, skill.AttackType, damage, t, skill.Critical);
                    skill.Critical = false;
                }
                else
                {
                    // Buffs/debuffs/status effects - just set param directly
                    r.Param = damage;
                }
                r.MessageId = msg;
                r.Resolution = skill.HasMissMsg ? (byte)1 : (byte)0;
                if (r.Resolution != 1 && r.Resolution != 3)
                {
                    r.Knockback = skill.Knockback;
                    if (first && t.Hp > 0 && skill.PrimarySc != 0)
                    {
                        SkillchainOnHit(pet, t, r, r.Param, skill.PrimarySc, skill.SecondarySc, skill.TertiarySc);
                        first = false;
                    }
                }
                if (t is MobEntity tm)
                {
                    if (!tm.IsAlive || tm.IsDead) ClaimMob(tm, pet, now);
                    DirtyExp(tm, pet);
                }
                if (t is CharData pc && SessionOf(pc) is { } s) SendUpdateMask(s, pc);
            }

            if (st.Target is MobEntity primary && EntityExists(primary))
            {
                if (primary.Allegiance != pet.Allegiance)
                {
                    bool isAvatar = pet.PetType == PetType.Avatar;
                    bool isAtomosSkill = skill.Id == AbilityDeconstruction;
                    bool isDead = !primary.IsAlive || primary.IsDead;
                    if (isDead || isAvatar && !isAtomosSkill) ClaimMob(primary, pet, now);
                }
                DirtyExp(primary, pet);
            }
            ProcessActionEffectFlags(pet, action);
        }

        // ActionInterrupts::AvatarOutOfRange / WyvernOutOfRange: the too-far-away finish, then the skill interrupt
        private void PetSkillOutOfRange(PetEntity pet, MobSkill skill, BattleEntity target)
        {
            var finish = new ActionPacket { ActorId = pet.Id, ActionType = ActionCategory.MagicFinish };
            var r = finish.AddTarget(target.Id).AddResult();
            if (pet.PetType == PetType.Wyvern) r.Resolution = 1; // ActionResolution::Miss
            r.Animation = AnimSkillInterrupt;
            r.Param = skill.MobSkillId > 0 ? skill.MobSkillId : skill.Id;
            r.MessageId = MsgTooFarAwayRed;
            PushMobInRange(pet, Battle2Packets.Battle2(finish));
            var interrupt = new ActionPacket { ActorId = pet.Id, ActionType = ActionCategory.SkillStart, ActionId = (uint)FourCC.SkillInterrupt };
            interrupt.AddTarget(pet.Id).AddResult().Animation = AnimSkillInterrupt;
            PushMobInRange(pet, Battle2Packets.Battle2(interrupt));
        }

        // luautils::OnPetAbility: the skill script's onPetAbility(target, pet, skill, master, action), whose number is
        // the damage; an avatar's skill gives its SMN master a summoning magic skill-up try
        private int OnPetAbility(BattleEntity target, PetEntity pet, MobSkill skill, BattleEntity master, ActionPacket action)
        {
            if (lua_ == null) return 0;
            int top = lua_.Top;
            int result;
            try
            {
                scripts_.PushFileFunction(PetSkillScript(skill), "onPetAbility");
                if (lua_.IsNil(-1)) return 0;
                lua_.Push(target);
                lua_.Push(pet);
                lua_.Push(skill);
                if (master != null) lua_.Push(master);
                else lua_.PushNil();
                lua_.Push(action);
                lua_.Call(5, 1);
                result = lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : 0;
            }
            catch (LuaException e)
            {
                log_($"luautils::onPetAbility: {e.Message}");
                return 0;
            }
            finally
            {
                lua_.SetTop(top);
            }
            if (pet.PMaster is CharData c && pet.PetType == PetType.Avatar && c.MJob == MobStats.JobSmn)
                TrySkillUp(c, Spell.SkillSummoning, c.MLvl, false, false);
            return result;
        }
    }
}
