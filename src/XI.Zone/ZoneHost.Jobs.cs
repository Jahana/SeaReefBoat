using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // CLuaBaseEntity's job, level and skill methods: changeJob, changesJob, unlockJob, hasJob, setLevel, setsLevel,
    // setLevelCap, levelRestriction, recalculateStats, setSkillLevel, setSkillRank, capSkill, capAllSkills,
    // recalculateSkillsTable, recalculateAbilitiesTable, the learned weapon skills and the skill reads. changeJob and
    // levelRestriction recheck the gear sets (CheckForGearSet); levelRestriction recalculates the pet (a charmed mob over
    // the level is let go) and replays the automaton's attachments around it.
    // changeJob enters and leaves Monstrosity (monstrosity::HandleJobChange, ZoneHost.Monstrosity.cs). changeJob and
    // changesJob drop the ability recasts (ChangeJob). The traits are rebuilt where C++ does (ZoneHost.Traits). A mob
    // given changeJob gets the job's weapon and its stats recalculated.
    public sealed partial class ZoneHost
    {
        private void AddJobBindings(Dictionary<string, LuaFunction> m)
        {
            m["changeJob"] = l =>
            {
                byte job = (byte)l.ToIntArg(2);
                if (Entity(l) is CharData c) ChangeJob(c, job);
                else if (Entity(l) is MobEntity mob) ChangeMobJob(mob, job);
                else log_("CLuaBaseEntity::changeJob() - m_pBaseEntity is not a supported entity.");
                return 0;
            };
            m["changesJob"] = l =>
            {
                byte job = (byte)l.ToIntArg(2);
                if (Entity(l) is CharData c) ChangeSubJob(c, job);
                else log_("Invalid entity type calling function (changesJob).");
                return 0;
            };
            m["unlockJob"] = l =>
            {
                int job = (byte)l.ToIntArg(2);
                if (Entity(l) is not CharData c)
                {
                    log_("CLuaBaseEntity::unlockJob() - Non-PC calling function.");
                    return 0;
                }
                if (job < MaxJobType)
                {
                    c.UnlockedJobs |= 1u << job;
                    if (job == 0) job = 1;
                    if (c.JobLevels[job] == 0) c.JobLevels[job] = 1;
                    SaveCharJob(c, job);
                    PushTo(c, GameOkPackets.JobInfo(c, UnixNow()));
                }
                return 0;
            };
            m["hasJob"] = l =>
            {
                int job = (byte)l.ToIntArg(2);
                if (Entity(l) is CharData c && job < MaxJobType) return Ret(l, (c.UnlockedJobs >> job & 1) != 0);
                log_("CLuaBaseEntity::unlockJob() - Non-PC calling function or invalid JOBTYPE");
                return Ret(l, false);
            };
            m["setLevel"] = l =>
            {
                byte level = (byte)l.ToIntArg(2);
                if (Entity(l) is not CharData c) log_("Attempt to change Main Job level for non-PC.");
                else if (level > 99) log_($"Attempt to set player level ({level}) that exceeds maximum.");
                else SetLevel(c, level);
                return 0;
            };
            m["setsLevel"] = l =>
            {
                byte level = (byte)l.ToIntArg(2);
                if (Entity(l) is not CharData c) log_("Invalid entity type calling function (setsLevel).");
                else if (level > 99) log_($"Invalid slevel ({level}) passed to function.");
                else SetSubLevel(c, level);
                return 0;
            };
            m["setLevelCap"] = l =>
            {
                byte cap = (byte)l.ToIntArg(2);
                if (Pc(l, "setLevelCap") is CharData c && c.Genkai != cap)
                {
                    c.Genkai = cap;
                    Exec("UPDATE char_jobs SET genkai = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.Genkai), BoundValue.Of(c.Id));
                }
                return 0;
            };
            m["levelRestriction"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("CLuaBaseEntity::levelRestriction() - Entity is null, or not PC.");
                    return Ret(l, 0);
                }
                if (l.IsNumber(2)) LevelRestriction(c, (uint)l.ToIntegral(2));
                return Ret(l, (byte)c.LevelRestriction);
            };
            m["recalculateStats"] = l =>
            {
                if (Entity(l) is CharData c) RecalculateStats(c);
                return 0;
            };

            m["getSkillLevel"] = l =>
            {
                int skill = (ushort)l.ToIntArg(2);
                // any battle entity: a mob's are mobutils' caps for its level and jobs
                if (Entity(l) is not BattleEntity b)
                {
                    log_("Invalid entity calling function (getSkillLevel).");
                    return Ret(l, 0);
                }
                if (skill >= Skill.Max)
                {
                    log_($"skillId ({skill}) exceeds MAX_SKILLTYPE.");
                    return Ret(l, 0);
                }
                return Ret(l, Melee.GetSkill(b, skill));
            };
            m["getMaxSkillLevel"] = l =>
            {
                var growth = loader_.Stats?.Growth;
                int level = (byte)l.ToIntArg(2), job = (byte)l.ToIntArg(3), skill = (byte)l.ToIntArg(4);
                return Ret(l, growth != null && skill < Skill.Max && job < MaxJobType ? growth.GetMaxSkill(skill, job, level) : 0);
            };
            m["getSkillRank"] = l =>
            {
                int skill = (byte)l.ToIntArg(2);
                return Ret(l, Entity(l) is CharData c && skill < Skill.Max ? c.RealSkillRank[skill] : 0);
            };
            m["getCharSkillLevel"] = l =>
            {
                int skill = (byte)l.ToIntArg(2);
                return Ret(l, Entity(l) is CharData c && skill < Skill.Max ? c.RealSkills[skill] : 0);
            };
            m["setSkillLevel"] = l =>
            {
                int skill = (byte)l.ToIntArg(2);
                ushort amount = (ushort)l.ToIntArg(3);
                if (Entity(l) is not CharData c || skill >= Skill.Max)
                {
                    log_("CLuaBaseEntity::setSkillLevel() - Non-PC or Invalid SkillID passed to function.");
                    return 0;
                }
                c.RealSkills[skill] = amount;
                c.WorkingSkills[skill] = (ushort)(amount / 10 * 0x20 + c.WorkingSkillRank[skill]);
                RefreshSkills(c, skill, true);
                return 0;
            };
            m["setSkillRank"] = l =>
            {
                int skill = (byte)l.ToIntArg(2);
                byte rank = (byte)l.ToIntArg(3);
                if (Entity(l) is CharData c && skill < Skill.Max)
                {
                    c.WorkingSkillRank[skill] = rank;
                    c.WorkingSkills[skill] = (ushort)(c.RealSkills[skill] / 10 * 0x20 + rank);
                    c.RealSkillRank[skill] = rank;
                    RefreshSkills(c, skill);
                }
                return 0;
            };

            m["capSkill"] = l =>
            {
                int skill = (byte)l.ToIntArg(2);
                if (skill >= Skill.Max) return 0;
                if (Entity(l) is not CharData c)
                {
                    log_("CLuaBaseEntity::capSkill() - m_PBaseEntity is not a player.");
                    return 0;
                }
                CapSkill(c, skill);
                PushTo(c, SelfPackets.CliStatus2(c));
                CheckWeaponSkill(c, skill);
                SaveCharSkill(c, skill);
                return 0;
            };
            m["capAllSkills"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                for (int i = Skill.HandToHand; i <= Skill.Handbell; i++)
                {
                    // the row is written as 5000 whatever the cap
                    Exec("INSERT INTO char_skills SET charid = ?, skillid = ?, value = ?, rank = ? ON DUPLICATE KEY UPDATE value = ?, rank = ?",
                         BoundValue.Of(c.Id), BoundValue.Of((byte)i), BoundValue.Of((ushort)5000), BoundValue.Of(c.RealSkillRank[i]),
                         BoundValue.Of((ushort)5000), BoundValue.Of(c.RealSkillRank[i]));
                    CapSkill(c, i);
                }
                CheckWeaponSkill(c, Skill.None);
                PushTo(c, SelfPackets.CliStatus2(c));
                return 0;
            };

            m["addLearnedWeaponskill"] = l => SetLearnedWeaponskill(l, true);
            m["delLearnedWeaponskill"] = l => SetLearnedWeaponskill(l, false);
            m["hasLearnedWeaponskill"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return Ret(l, false);
                }
                return Ret(l, CharStats.HasLearnedWeaponskill(c, (byte)l.ToIntArg(2)));
            };

            m["recalculateSkillsTable"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                loader_.Stats?.BuildSkillsTable(c);
                loader_.Stats?.BuildWeaponSkills(c);
                PushTo(c, SelfPackets.CliStatus2(c));
                PushTo(c, AbilRecast(c));
                PushTo(c, LoginPackets.CommandData(c));
                return 0;
            };
            m["recalculateAbilitiesTable"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                loader_.BuildAbilityTable(c);
                BuildCharTraits(c);
                loader_.Stats?.BuildWeaponSkills(c);
                if (c.PPet is PetEntity pet) BuildPetAbilityTable(c, pet, pet.PetId);
                PushTo(c, LoginPackets.CommandData(c));
                return 0;
            };
        }

        // capSkill's cap: the real skill at the job's cap and the working skill flagged blue
        private void CapSkill(CharData c, int skill)
        {
            int maxSkill = 10 * (loader_.Stats?.Growth?.GetMaxSkill(skill, c.MJob, c.MLvl) ?? 0);
            c.RealSkills[skill] = (ushort)maxSkill;
            c.WorkingSkills[skill] = (ushort)(maxSkill / 10 | 0x8000);
        }

        // add / delLearnedWeaponskill: the 64-bit learned set, the weapon skill lists, the row and COMMAND_DATA
        private int SetLearnedWeaponskill(LuaState l, bool learned)
        {
            if (Entity(l) is not CharData c)
            {
                log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                return 0;
            }
            int id = (byte)l.ToIntArg(2);
            if (id < 64)
            {
                if (learned) c.LearnedWeaponskills[id / 8] |= (byte)(1 << id % 8);
                else c.LearnedWeaponskills[id / 8] &= (byte)~(1 << id % 8);
            }
            loader_.Stats?.BuildWeaponSkills(c);
            SaveLearnedAbilities(c);
            PushTo(c, LoginPackets.CommandData(c));
            return 0;
        }

        // charutils::CheckWeaponSkill: a skill level just reached teaches the weapon skills at exactly that level, when
        // the main or ranged weapon is of that skill
        internal void CheckWeaponSkill(CharData c, int skill)
        {
            var stats = loader_.Stats;
            if (stats == null || skill < 0 || skill >= Skill.Max) return;
            if (CharStats.WeaponIn(c, CharStats.SlotMain)?.Skill != skill && CharStats.WeaponIn(c, CharStats.SlotRanged)?.Skill != skill) return;

            int curSkill = c.RealSkills[skill] / 10;
            foreach (var ws in stats.WeaponSkills.ForSkill(skill))
            {
                if (curSkill != ws.SkillLevel || !CharStats.CanUseWeaponskill(c, ws)) continue;
                CharStats.AddWeaponSkill(c, ws.Id);
                PushTo(c, SelfPackets.BattleMessage(c, ws.Id, ws.Id, MsgBasicLearnsAbility));
                PushTo(c, LoginPackets.CommandData(c));
            }
        }

        private const ushort MsgBasicLearnsAbility = 45;

        // setSkillLevel / setSkillRank's tail: the gifts, the skill table, the row and CLISTATUS2
        private void RefreshSkills(CharData c, int skill, bool checkWeaponSkill = false)
        {
            RefreshGiftMods(c);
            loader_.Stats?.BuildSkillsTable(c);
            if (checkWeaponSkill) CheckWeaponSkill(c, skill);
            SaveCharSkill(c, skill);
            PushTo(c, SelfPackets.CliStatus2(c));
        }

        // jobpointutils::RefreshGiftMods with the MAGIC_DATA a learned job point spell sends
        private void RefreshGiftMods(CharData c)
        {
            if (loader_.RefreshGiftMods(c)) PushTo(c, LoginPackets.MagicData(c));
        }

        // CLuaBaseEntity::changeJob for a char: the main job only, its level left as it was
        private void ChangeJob(CharData c, byte newJob)
        {
            var stats = loader_.Stats;
            if (stats == null) return;
            byte prevJob = c.MJob;
            ResetPetZoningInfo(c);

            stats.RemoveAllEquipMods(c);
            c.UnlockedJobs |= 1u << newJob;
            if (newJob == 0 || newJob >= MaxJobType) log_($"Invalid Job Type passed to function ({newJob}).");
            else c.MJob = newJob;
            MonstrosityJobChange(c, newJob);
            stats.ApplyAllEquipMods(c);
            LoadAutomaton(c);

            if (newJob == JobBluId)
            {
                if (prevJob != JobBluId) LoadSetSpells(c);
            }
            else if (c.SJob != JobBluId && SessionOf(c) is MapSession bs) UnequipAllBlueSpells(bs, c);

            if (SessionOf(c) is MapSession s) SetStyleLock(s, c, false);
            CheckForGearSet(c); // check for gear set on gear change
            RefreshGiftMods(c);
            stats.BuildSkillsTable(c);
            loader_.CalculateStats(c);
            stats.CheckValidEquipment(c);
            (c.Recasts as CharRecastContainer)?.ChangeJob();
            loader_.BuildAbilityTable(c);
            BuildCharTraits(c);
            LatentsForParty(c, m => m.Latents.CheckLatentsPartyJobs());

            CharStats.UpdateHealth(c);
            c.Hp = c.MaxHp;
            c.Mp = c.MaxMp;
            SaveCharStats(c);
            SaveCharJob(c, c.MJob);
            SaveCharExp(c, c.MJob);
            c.UpdateMask |= CharStats.UpdateHp;
            PushJobChangePackets(c, stats, true);
        }

        // CLuaBaseEntity::changeJob for a mob: the main job, a fresh main weapon of the job's skill and delay (240, or
        // 480 for the slow ones; no damage type), then mobutils::CalculateMobStats
        private void ChangeMobJob(MobEntity mob, byte newJob)
        {
            if (newJob == 0 || newJob >= MaxJobType) log_($"Invalid Job Type passed to function ({newJob}).");
            else mob.MJob = newJob;

            // Change weapon type based on new job
            int skill = Skill.None;
            ushort delay = 240;
            switch (newJob)
            {
                case MobStats.JobMnk:
                case JobPupId:
                    skill = Skill.HandToHand;
                    delay = 480;
                    break;
                case MobStats.JobThf:
                case 10: // BRD
                case 11: // RNG
                case 17: // COR
                    skill = Skill.Dagger;
                    break;
                case MobStats.JobRdm:
                case MobStats.JobPld:
                case JobBluId:
                    skill = Skill.Sword;
                    break;
                case 22: // RUN
                    skill = Skill.GreatSword;
                    delay = 480;
                    break;
                case MobStats.JobWar:
                case 9: // BST
                    skill = Skill.Axe;
                    break;
                case MobStats.JobDrk:
                    skill = Skill.Scythe;
                    delay = 480;
                    break;
                case 14: // DRG
                    skill = Skill.Polearm;
                    delay = 480;
                    break;
                case 13: // NIN
                    skill = Skill.Katana;
                    break;
                case 12: // SAM
                    skill = Skill.GreatKatana;
                    delay = 480;
                    break;
                case MobStats.JobWhm:
                case MobStats.JobBlm:
                case 21: // GEO
                    skill = Skill.Club;
                    break;
                case 15: // SMN
                    skill = Skill.Staff;
                    delay = 480;
                    break;
            }
            mob.WeaponSkill = (byte)skill;
            mob.MainDelay = mob.BaseDelay = delay;
            if (mob is PetEntity pet) pet.DmgType = 0; // xi::DamageType::None
            MobStatsFor().CalculateMobStats(mob, true, AreaOf(mob).Types.Contains("instanced"));
        }

        // CLuaBaseEntity::changesJob: the sub job, its level left as it was, then charutils::UpdateSubJob
        private void ChangeSubJob(CharData c, byte subJob)
        {
            var stats = loader_.Stats;
            if (stats == null) return;
            c.UnlockedJobs |= 1u << subJob;
            if (subJob >= MaxJobType) log_($"sjob ({subJob}) exceeds MAX_JOBTYPE");
            else c.SJob = subJob;
            LoadAutomaton(c);
            UpdateSubJob(c, stats);

            if (subJob == JobBluId) LoadSetSpells(c);
            else if (SessionOf(c) is MapSession bs) UnequipAllBlueSpells(bs, c);
            if (SessionOf(c) is MapSession s) SendExtendedJobPackets(s, c);
        }

        // charutils::UpdateSubJob
        private void UpdateSubJob(CharData c, CharStats stats)
        {
            RefreshGiftMods(c);
            stats.BuildSkillsTable(c);
            loader_.CalculateStats(c);
            stats.CheckValidEquipment(c);
            (c.Recasts as CharRecastContainer)?.ChangeJob();
            loader_.BuildAbilityTable(c);
            BuildCharTraits(c);

            CharStats.UpdateHealth(c);
            c.Hp = c.MaxHp;
            c.Mp = c.MaxMp;
            SaveCharStats(c);
            SaveCharJob(c, c.MJob);
            SaveCharExp(c, c.MJob);
            c.UpdateMask |= CharStats.UpdateHp;
            PushJobChangePackets(c, stats, false);
        }

        // CLuaBaseEntity::setLevel: the main job's level, its exp one short of the next
        private void SetLevel(CharData c, byte level)
        {
            var stats = loader_.Stats;
            if (stats == null) return;
            stats.RemoveAllEquipMods(c);
            SetMLevel(c, level);
            c.JobLevels[c.MJob] = level;
            SetSLevel(c, c.JobLevels[c.SJob], stats.Settings.SubjobRatio);
            c.JobExp[c.MJob] = (ushort)(stats.ExpNextLevelFor(c, level) - 1);
            stats.ApplyAllEquipMods(c);
            if (SessionOf(c) is MapSession s) SetStyleLock(s, c, false);
            ValidateBlueSpells(c);
            loader_.CalculateStats(c);
            stats.CheckValidEquipment(c);
            RefreshGiftMods(c);
            stats.BuildSkillsTable(c);
            loader_.BuildAbilityTable(c);
            stats.BuildWeaponSkills(c);
            BuildCharTraits(c);

            CharStats.UpdateHealth(c);
            c.Hp = c.MaxHp;
            c.Mp = c.MaxMp;
            SaveCharStats(c);
            SaveCharJob(c, c.MJob);
            SaveCharExp(c, c.MJob);
            c.UpdateMask |= CharStats.UpdateHp;
            PushJobChangePackets(c, stats, false);
        }

        // CLuaBaseEntity::setsLevel: the sub job's level (no UPDATE_HP, as C++)
        private void SetSubLevel(CharData c, byte level)
        {
            var stats = loader_.Stats;
            if (stats == null) return;
            c.JobLevels[c.SJob] = level;
            SetSLevel(c, c.JobLevels[c.SJob], stats.Settings.SubjobRatio);
            c.JobExp[c.SJob] = (ushort)(stats.Growth.GetExpNextLevel(c.JobLevels[c.SJob]) - 1);
            if (SessionOf(c) is MapSession s) SetStyleLock(s, c, false);
            RefreshGiftMods(c);
            stats.BuildSkillsTable(c);
            loader_.CalculateStats(c);
            stats.CheckValidEquipment(c);
            loader_.BuildAbilityTable(c);
            BuildCharTraits(c);

            CharStats.UpdateHealth(c);
            c.Hp = c.MaxHp;
            c.Mp = c.MaxMp;
            SaveCharStats(c);
            SaveCharJob(c, c.MJob);
            SaveCharJob(c, c.SJob);
            SaveCharExp(c, c.MJob);
            PushJobChangePackets(c, stats, false);
        }

        // CParty::RefreshSync's level change: the main level set to the sync, the sub job's from its own level
        private void ApplySyncLevel(CharData c, byte level)
        {
            var stats = loader_.Stats;
            if (stats == null) return;
            stats.RemoveAllEquipMods(c);
            SetMLevel(c, level);
            SetSLevel(c, c.JobLevels[c.SJob], stats.Settings.SubjobRatio);
            stats.ApplyAllEquipMods(c);
            ValidateBlueSpells(c);
            RefreshGiftMods(c);
            stats.BuildSkillsTable(c);
            loader_.CalculateStats(c);
            BuildCharTraits(c);
            loader_.BuildAbilityTable(c);
            stats.BuildWeaponSkills(c);
            stats.CheckValidEquipment(c);
            PushTo(c, LoginPackets.CommandData(c));
        }

        // CLuaBaseEntity::levelRestriction: the main level capped by the restriction (0 lifts it). Only the char's own
        // half: no pets to detach or recalculate.
        private void LevelRestriction(CharData c, uint restriction)
        {
            var stats = loader_.Stats;
            if (stats == null) return;
            c.LevelRestriction = restriction;
            byte jobLevel = c.JobLevels[c.MJob];
            byte newLevel = restriction != 0 && restriction < jobLevel ? (byte)restriction : jobLevel;
            if (c.MLvl == newLevel) return;

            // Call each attachment onUnequip handler and zero out localVars tracking applied buffs
            // (puppetutils::PreLevelRestriction)
            if (c.PPet is AutomatonEntity automaton) AutomatonAttachmentHooks(automaton, "onUnequip");
            stats.RemoveAllEquipMods(c);
            SetMLevel(c, newLevel);
            SetSLevel(c, c.JobLevels[c.SJob], stats.Settings.SubjobRatio);
            stats.ApplyAllEquipMods(c);
            ValidateBlueSpells(c);
            RefreshGiftMods(c);
            stats.BuildSkillsTable(c);
            loader_.CalculateStats(c);
            BuildCharTraits(c);
            loader_.BuildAbilityTable(c);
            stats.CheckValidEquipment(c);
            CheckForGearSet(c);
            c.UpdateMask |= CharStats.UpdateHp;

            // Update the character's Automaton capacity bonus regardless if the pet is out or not
            c.Automaton.SetElementalCapacityBonus((byte)c.GetMod(Mod.AUTO_ELEM_CAPACITY));

            PushTo(c, GameOkPackets.JobInfo(c, UnixNow()));
            PushTo(c, SelfPackets.CliStatus(c, stats, UnityStatus(c)));
            PushTo(c, SelfPackets.CliStatus2(c));
            PushTo(c, AbilRecast(c));
            PushTo(c, LoginPackets.CommandData(c));
            PushTo(c, LoginPackets.MagicData(c));
            PushTo(c, CharStatus(c));
            PushTo(c, CharSyncOf(c));

            if (c.PPet != null) LevelRestrictPet(c, newLevel);
        }

        // levelRestriction's pet half: a charmed mob over the new level is let go; a pet is recalculated from a clean
        // slate for its type (an automaton over its elemental capacity, or a pet of another type, is despawned), then
        // onPetLevelRestriction, SetupPetWithMaster, and its HP / MP as they were (capped at the new maxima)
        private void LevelRestrictPet(CharData c, byte newLevel)
        {
            if (c.PPet is not PetEntity pet)
            {
                // Charmed mobs only detach if they are above the level restriction
                if (c.PPet.MLvl > newLevel) DetachPet(c);
                return;
            }

            // Preserve pet's HP and MP
            int hp = pet.Hp, mp = pet.Mp;

            // Reset pet to a clean slate
            pet.StatusEffects.KillAllStatusEffect();
            pet.RestoreModifiers();
            pet.RestoreMobModifiers();
            pet.TraitList.Clear();

            var data = PetList().GetValueOrDefault(pet.PetId);
            switch (pet.PetType)
            {
                case PetType.Avatar when data != null:
                    CalculateAvatarStats(c, pet, data);
                    break;
                case PetType.Wyvern when data != null:
                    CalculateWyvernStats(c, pet, data);
                    break;
                case PetType.JugPet when data != null:
                    CalculateJugPetStats(c, pet, data);
                    break;
                case PetType.Automaton when data != null:
                    if (IsExceedingElementalCapacity(c))
                    {
                        // Reset Activate if Automaton is full HP
                        if (pet.Hp == pet.MaxHp) ResetRecast(c, RecastType.Ability, 205);
                        PushTo(c, SelfPackets.BattleMessage(c, 0, 0, MsgAutoExceedsCapacity));
                        DespawnPet(c);
                        return;
                    }
                    CalculatePetAutomatonStats(c, (AutomatonEntity)pet, data);
                    // Call each attachment onEquip handler and replay maneuvers against the new stats
                    // (puppetutils::PostLevelRestriction)
                    AutomatonAttachmentHooks((AutomatonEntity)pet, "onEquip");
                    UpdateAttachments(c);
                    break;
                case PetType.Luopan:
                    CalculateLuopanStats(c, pet);
                    break;
                default:
                    DespawnPet(c);
                    return;
            }

            // Allow global pet script to handle the level restriction
            CallMobHook(pet, "onPetLevelRestriction", pet);

            // Setup pet with master since traits, abilities and some status effects need to be reapplied
            SetupPetWithMaster(c, pet);

            // Restore pet's HP and MP to what it was before the stat recalculation
            pet.Hp = Math.Min(hp, pet.MaxHp);
            pet.Mp = Math.Min(mp, pet.MaxMp);
            pet.UpdateMask |= EntityUpdatePacket.UpdateHp;
        }

        private const ushort MsgAutoExceedsCapacity = 745; // Your automaton exceeds one or more elemental capacity values

        // CLuaBaseEntity::isExceedingElementalCapacity: an element's equipped load over its capacity
        private static bool IsExceedingElementalCapacity(CharData c)
        {
            for (int i = 0; i < 8; i++)
                if (c.Automaton.ElementEquip[i] > c.Automaton.ElementMax[i]) return true;
            return false;
        }

        // puppetutils::PreLevelRestriction / PostLevelRestriction's loop: each attachment set's onUnequip / onEquip
        private void AutomatonAttachmentHooks(AutomatonEntity pet, string hook)
        {
            for (int i = 0; i < 12; i++)
                if (pet.Attachments[i] != 0 && PuppetItem(0x2100 + pet.Attachments[i]) != null) AttachmentHook(pet, pet.Attachments[i], hook);
        }

        // CLuaBaseEntity::recalculateStats (HP / MP left as they are)
        private void RecalculateStats(CharData c)
        {
            var stats = loader_.Stats;
            if (stats == null) return;
            RefreshGiftMods(c);
            stats.BuildSkillsTable(c);
            loader_.CalculateStats(c);
            BuildCharTraits(c);
            stats.CheckValidEquipment(c);
            loader_.BuildAbilityTable(c);
            CharStats.UpdateHealth(c);
            PushJobChangePackets(c, stats, false);
        }

        // the packets changeJob, UpdateSubJob, setLevel, setsLevel and recalculateStats end with
        private void PushJobChangePackets(CharData c, CharStats stats, bool extendedJob)
        {
            PushTo(c, GameOkPackets.JobInfo(c, UnixNow()));
            PushTo(c, SelfPackets.CliStatus(c, stats, UnityStatus(c)));
            PushTo(c, SelfPackets.CliStatus2(c));
            PushTo(c, AbilRecast(c));
            PushTo(c, LoginPackets.CommandData(c));
            PushTo(c, CharStatus(c));
            if (extendedJob && SessionOf(c) is MapSession s) SendExtendedJobPackets(s, c);
            PushTo(c, SelfPackets.Merits(c, stats));
            PushTo(c, MonPage1(c));
            PushTo(c, GameOkPackets.Monstrosity2(c));
            PushTo(c, CharSyncOf(c));
        }
    }
}
