using System;
using System.Collections.Generic;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // Player pets: petutils' CPetEntity half (LoadPet with the avatar, wyvern and jug pet stats, SpawnPet,
    // SetupPetWithMaster, FinalizePetStatistics, DetachPet, DespawnPet), CZoneEntities' pet list (InsertPET under a
    // dynamic targid, SpawnPETs, petTick, the targid freed 60 s after the pet goes), CPetEntity (Spawn, Die, PostTick's
    // updates and 0x068 pet sync, FadeOut), CPetController's roam (following the master, a jug pet's time running out,
    // dying with its master) and the char's zone-out (CZoneEntities::DecreaseZoneCounter). A pet's script is
    // xi.pets.<avatar|wyvern|jug>. Pet melee: petAttack engages the pet on a mob its master may claim
    // (CPetEntity::CanAttack), the controller's combat tick chases and swings, its hits claim the mob for the master
    // and put the mob's enmity on the pet, and mobs fight back at pets (ZoneHost.MobAi.cs takes any BattleEntity
    // target); the avatar's perpetuation drains its master's MP each regen tick.
    // A pet that no path reaches its master from is warped next to it (CPathFind::WarpTo).
    // An automaton is in ZoneHost.Automatons.cs; the pet's spells go through the mob casting AI (ZoneHost.MobMagic.cs,
    // the Light Spirit's idle buffs here), its blood pacts / ready moves and the char's pet commands are
    // ZoneHost.PetSkills.cs, petZoningInfo ZoneHost.PetZoning.cs.
    // Not reached, as in C++: a chocobo pet (pet 74 has no pet_list row, so LoadPet stops at the lookup) and
    // map.DESPAWN_JUGPETS_BELOW_MINIMUM_LEVEL (no zone has a level restriction of its own: the zone files give none).
    public sealed partial class ZoneHost
    {
        private const float PetRoamDistance = 2.1f;   // CPetController::PetRoamDistance
        private const long PetDeathMs = 2500;         // CPetEntity::Die's Internal_Die(2500ms)
        private const long PetDespawnMs = 3000;       // CDespawnState's wait before the fade
        private const byte PetModAvatar = 1, PetModWyvern = 2, PetModLuopan = 8;
        private const ushort MeritAvatarPhysicalAccuracy = 1280, MeritAvatarPhysicalAttack = 1282,
            MeritAvatarMagicalAccuracy = 1284, MeritAvatarMagicalAttack = 1286, MeritBeastAffinity = 2564;
        private const int JpWyvernMaxHpBonus = 0x1C0 + 0x05, JpSummonAccBonus = 0x1E0 + 0x01, JpSummonMagicAccBonus = 0x1E0 + 0x03,
            JpSummonPhysAtkBonus = 0x1E0 + 0x05, JpSummonMagicDmgBonus = 0x1E0 + 0x08, JpBloodPactDmgBonus = 0x1E0 + 0x09;
        private const int TraitStoutServant = 103;
        private const ushort EffectOmerta = 262, EffectDebilitation = 263; // EffectImpairment is ZoneHost.MobSkills.cs'

        // grades.cpp's HPScale (base, <30, <60, <75, >75) and MPScale (base, <60, >60) per rank, which only pets use
        private static readonly float[,] PetHpScale =
        {
            { 0, 0, 0, 0, 0 }, { 19, 9, 1, 3, 3 }, { 17, 8, 1, 3, 3 }, { 16, 7, 1, 3, 3 }, { 14, 6, 0, 3, 3 },
            { 13, 5, 0, 2, 2 }, { 11, 4, 0, 2, 2 }, { 10, 3, 0, 2, 2 },
        };
        private static readonly float[,] PetMpScale =
        {
            { 0, 0, 0 }, { 16, 6, 4 }, { 14, 5, 4 }, { 12, 4, 4 }, { 10, 3, 4 }, { 8, 2, 3 }, { 6, 1, 2 }, { 4, 0.5f, 1 },
        };

        // ---- LoadPet -----------------------------------------------------------------------------------------------

        // petutils::LoadPet: a new pet beside its master, its stats from the pet_list row and the master. The wyvern's
        // name comes from char_pet and, as in the C++, is written into the shared pet list.
        private void LoadPet(CharData master, int petId)
        {
            if (petId >= PetId.MaxPetId)
            {
                log_($"PetID ({petId}) exceeds MAX_PETID");
                return;
            }
            if (!PetList().TryGetValue(petId, out var data))
            {
                log_($"Could not look up pet data for id: {petId}");
                return;
            }
            if (master.MJob != MobStats.JobDrg && petId == PetId.Wyvern) return;
            master.PetZoning.PetId = petId;

            var petType = PetType.JugPet;
            if (petId <= PetId.CaitSith || petId == PetId.Siren) petType = PetType.Avatar;
            else if (petId == PetId.Wyvern)
            {
                petType = PetType.Wyvern;
                var r = Exec("SELECT pet_name.name, char_pet.wyvernid FROM pet_name, char_pet WHERE pet_name.id = char_pet.wyvernid AND " +
                             "char_pet.charid = ?", BoundValue.Of(master.Id));
                if (r != null && r.Next() && r.Get<ushort>("wyvernid") != 0) data.Name = r.Get<string>("name");
            }
            else if (petId == PetId.Chocobo) petType = PetType.Chocobo;
            else if (petId >= PetId.HarlequinFrame && petId <= PetId.StormwakerFrame) petType = PetType.Automaton;
            else if (petId == PetId.Luopan) petType = PetType.Luopan;

            if (petType == PetType.Chocobo)
            {
                log_($"LoadPet: {petType} pets are not ported ({master.Name}, pet {petId})");
                return;
            }

            var pet = petType == PetType.Automaton ? new AutomatonEntity(petId) : new PetEntity(petType, petId);
            // its CStatusEffectContainer
            AttachEffects(pet);
            if (petType != PetType.Automaton) pet.SaveModifiers();
            pet.Zone = master.Zone;
            // a luopan at the target's position plus the cast packet's offset (m_ActionOffsetPos), the Alexander /
            // Odin / Atomos on the master, the others behind it
            if (petType == PetType.Luopan) (pet.X, pet.Y, pet.Z) = (master.ActionOffsetX, master.ActionOffsetY, master.ActionOffsetZ);
            else
                (pet.X, pet.Y, pet.Z) = petId == PetId.Alexander || petId == PetId.Odin || petId == PetId.Atomos
                    ? NearPosition(master, 0, master.Rotation)
                    : NearPosition(master, PetRoamDistance, MathF.PI);
            pet.Rotation = master.Rotation;
            pet.Moving = master.Moving;

            if (petType != PetType.Automaton)
            {
                pet.Look = (ushort[])data.Look.Clone();
                pet.Name = data.Name;
            }
            pet.NamePrefix = data.NamePrefix;
            pet.Species = data.Species;
            pet.MobSkillList = data.MobSkillList;
            pet.MJob = data.MJob;
            pet.SJob = data.SJob;
            pet.Element = data.Element;
            pet.BaseSpeed = data.Speed;
            pet.AnimationSpeed = data.Speed;
            MobStats.UpdateSpeed(pet);
            foreach (var (mod, value) in data.Mods) pet.SetModifier(mod, value);

            switch (petType)
            {
                case PetType.Avatar:
                    CalculateAvatarStats(master, pet, data);
                    break;
                case PetType.JugPet:
                    // the level it zoned with, else any
                    pet.SpawnLevel = master.PetZoning.PetLevel > 0 ? master.PetZoning.PetLevel : byte.MaxValue;
                    pet.JugDurationMs = data.TimeSec * 1000L;
                    CalculateJugPetStats(master, pet, data);
                    break;
                case PetType.Wyvern:
                    CalculateWyvernStats(master, pet, data);
                    break;
                case PetType.Automaton:
                    LoadAutomaton(master);
                    CalculatePetAutomatonStats(master, (AutomatonEntity)pet, data);
                    EquipAttachments((AutomatonEntity)pet);
                    SetAutomatonController((AutomatonEntity)pet);
                    break;
                case PetType.Luopan:
                    CalculateLuopanStats(master, pet);
                    break;
            }

            pet.SpawnLevel = pet.MLvl;
            pet.Status = NpcEntity.StatusNormal;
            pet.ModelSize = data.ModelSize;
            pet.ModelHitboxSize = data.ModelHitboxSize;
            pet.Ecosystem = data.Ecosystem;
            if (petType == PetType.Wyvern || petType == PetType.JugPet) MobStats.AddEcosystemKillerEffects(pet);
            pet.DmgType = data.DmgType;
            master.PPet = pet;
        }

        // petutils::CalculatePetHPMP: a level-scaled player's HP and MP for race 3 and the pet's main job, plus the
        // bonus HP (and 6800 for Odin and Alexander)
        private void CalculatePetHPMP(PetEntity pet)
        {
            var grades = loader_.Grades;
            const int race = 3;
            int mlvl = pet.MLvl;
            int over30 = Math.Clamp(mlvl - 30, 0, 30);
            int upTo60 = mlvl < 60 ? mlvl - 1 : 59;
            int over60To75 = Math.Clamp(mlvl - 60, 0, 15);
            int over75 = mlvl < 75 ? 0 : mlvl - 75;
            int over10 = mlvl < 10 ? 0 : mlvl - 10;
            int over50Under60 = Math.Clamp(mlvl - 50, 0, 10);
            int over60 = mlvl < 60 ? 0 : mlvl - 60;

            float Hp(int g) => PetHpScale[g, 0] + PetHpScale[g, 1] * upTo60 + PetHpScale[g, 2] * over30 + PetHpScale[g, 3] * over60To75 +
                               PetHpScale[g, 4] * over75;
            float Mp(int g) => PetMpScale[g, 0] + PetMpScale[g, 1] * upTo60 + PetMpScale[g, 2] * over60;

            float raceStat = Hp(grades.GetRaceGrades(race, 0));
            float jobStat = Hp(grades.GetJobGrade(pet.MJob, 0));
            int bonusStat = (over10 + over50Under60) * 2;
            if (pet.PetId == PetId.Odin || pet.PetId == PetId.Alexander) bonusStat += 6800;
            pet.BaseMaxHp = (short)(raceStat + jobStat + bonusStat);
            pet.Hp = pet.BaseMaxHp;

            raceStat = 0;
            jobStat = 0;
            // the race brings MP only to a job with an MP grade
            if (grades.GetJobGrade(pet.MJob, 1) != 0) raceStat = Mp(grades.GetRaceGrades(race, 1));
            byte grade = grades.GetJobGrade(pet.MJob, 1);
            if (grade > 0) jobStat = Mp(grade);
            pet.BaseMaxMp = (short)(raceStat + jobStat);
            pet.Mp = pet.BaseMaxMp;
        }

        // the stats every pet type shares: the species' ranks and the main job's at the main level, half the sub job's
        // at sLvl
        private void SetPetBaseStats(PetEntity pet, PetInfo data, int sLvl)
        {
            var grades = loader_.Grades;
            byte[] ranks = { data.StrRank, data.DexRank, data.VitRank, data.AgiRank, data.IntRank, data.MndRank, data.ChrRank };
            for (int k = 0; k < 7; k++)
            {
                int f = MobStats.GetBaseToRank(ranks[k], pet.MLvl);
                int m = MobStats.GetBaseToRank(grades.GetJobGrade(pet.MJob, k + 2), pet.MLvl);
                int s = MobStats.GetBaseToRank(grades.GetJobGrade(pet.SJob, k + 2), sLvl) / 2;
                pet.Stats[k] = (ushort)(f + m + s);
            }
        }

        // the pet's level: SetMLevel's 1 for 0, SetSLevel's sub level no higher than the main for a pet
        private static void SetPetLevels(PetEntity pet, int mLvl, int? sLvl)
        {
            pet.MLvl = (byte)(mLvl == 0 ? 1 : mLvl);
            if (sLvl is int s) pet.SLvl = (byte)Math.Min(s, pet.MLvl);
        }

        // petutils::CalculateAvatarStats: the SMN's level (with the avatar level gear) or its sub level, 1:1 sub
        private void CalculateAvatarStats(CharData master, PetEntity pet, PetInfo data)
        {
            int mLvl = master.MLvl;
            int sLvl = mLvl;
            if (master.MJob == MobStats.JobSmn)
            {
                mLvl = (byte)(mLvl + master.GetMod(Mod.AVATAR_LVL_BONUS));
                if (pet.PetId == PetId.Carbuncle) mLvl = (byte)(mLvl + master.GetMod(Mod.CARBUNCLE_LVL_BONUS));
                else if (pet.PetId == PetId.CaitSith) mLvl = (byte)(mLvl + master.GetMod(Mod.CAIT_SITH_LVL_BONUS));
                SetPetLevels(pet, mLvl, mLvl);
            }
            else if (master.SJob == MobStats.JobSmn)
            {
                mLvl = master.SLvl;
                SetPetLevels(pet, mLvl, mLvl);
            }
            else if (pet.PetId == PetId.WaterSpirit)
            {
                // Poseidon's Ring on a non-SMN: the main level, capped at 75
                mLvl = Math.Min(mLvl, 75);
                SetPetLevels(pet, mLvl, mLvl);
            }
            else
            {
                log_($"{master.Name} summoned an avatar but is not SMN main or SMN sub! Please report. ");
                SetPetLevels(pet, 1, null);
            }

            CalculatePetHPMP(pet);
            SetPetBaseStats(pet, data, sLvl);
            pet.SpellList = GetMobSpellList(data.SpellList);
            pet.MainDelay = pet.BaseDelay = data.CmbDelay;
            pet.RangedBaseDelay = 360; // Titan's ranged skills' TP returns
            pet.MainDamage = pet.RangedDamage = (ushort)mLvl;

            var stats = MobStatsFor();
            pet.AddModifier(Mod.DEF, (short)MobStats.GetBaseDefEva(pet, data.DefRank));
            pet.AddModifier(Mod.EVA, (short)MobStats.GetBaseDefEva(pet, stats.JobSkillRankToBaseEvaRank(pet.MJob, pet.SJob)));
            pet.AddModifier(Mod.ATT, (short)stats.GetBaseSkill(pet, data.AttRank));
            pet.AddModifier(Mod.ACC, (short)stats.GetBaseSkill(pet, data.AccRank));
            pet.AddModifier(Mod.RATT, (short)stats.GetBaseSkill(pet, data.AttRank));
            pet.AddModifier(Mod.RACC, (short)stats.GetBaseSkill(pet, data.AccRank));

            // the magic skills at their caps, so the spell scripts play nice
            var growth = loader_.Stats.Growth;
            int cappedLvl = Math.Min(mLvl, 99);
            for (int i = PetSkillDivineMagic; i <= PetSkillBlueMagic; i++)
            {
                ushort maxSkill = growth.GetMaxSkill(i, pet.MJob, cappedLvl);
                if (maxSkill != 0) pet.WorkingSkills[i] = maxSkill;
                else
                {
                    ushort maxSubSkill = growth.GetMaxSkill(i, pet.SJob, cappedLvl);
                    if (maxSubSkill != 0) pet.WorkingSkills[i] = maxSubSkill;
                }
            }

            CallPetGlobal("xi.pets.avatar.onPetStatCalculate", master, pet);

            if (master.Merits != null)
            {
                pet.AddModifier(Mod.MATT, (short)master.Merits.Value(master, MeritAvatarMagicalAttack));
                pet.AddModifier(Mod.ATT, (short)master.Merits.Value(master, MeritAvatarPhysicalAttack));
                pet.AddModifier(Mod.MACC, (short)master.Merits.Value(master, MeritAvatarMagicalAccuracy));
                pet.AddModifier(Mod.ACC, (short)master.Merits.Value(master, MeritAvatarPhysicalAccuracy));
            }
            pet.AddModifier(Mod.ACC, (short)JobPointUtil.Value(master, JpSummonAccBonus));
            pet.AddModifier(Mod.MACC, (short)JobPointUtil.Value(master, JpSummonMagicAccBonus));
            pet.AddModifier(Mod.ATT, (short)(JobPointUtil.Value(master, JpSummonPhysAtkBonus) * 2));
            pet.AddModifier(Mod.MAGIC_DAMAGE, (short)(JobPointUtil.Value(master, JpSummonMagicDmgBonus) * 5));
            pet.AddModifier(Mod.BP_DAMAGE, (short)(JobPointUtil.Value(master, JpBloodPactDmgBonus) * 3));

            // the SMN job gifts
            if (master.MJob == MobStats.JobSmn)
            {
                pet.AddModifier(Mod.ATT, master.GetMod(Mod.PET_ATK_DEF));
                pet.AddModifier(Mod.DEF, master.GetMod(Mod.PET_ATK_DEF));
                pet.AddModifier(Mod.ACC, master.GetMod(Mod.PET_ACC_EVA));
                pet.AddModifier(Mod.EVA, master.GetMod(Mod.PET_ACC_EVA));
                pet.AddModifier(Mod.MATT, master.GetMod(Mod.PET_MAB_MDB));
                pet.AddModifier(Mod.MDEF, master.GetMod(Mod.PET_MAB_MDB));
                pet.AddModifier(Mod.MACC, master.GetMod(Mod.PET_MACC_MEVA));
                pet.AddModifier(Mod.MEVA, master.GetMod(Mod.PET_MACC_MEVA));
            }

            FinalizePetStatistics(master, pet);
        }

        private const int PetSkillGuard = 28, PetSkillParry = 31, PetSkillDivineMagic = 32, PetSkillBlueMagic = 43;

        // petutils::CalculateWyvernStats: a DRG/DRG at the master's level (with the main hand's item level past 99
        // and the wyvern level gear), sub level 1
        private void CalculateWyvernStats(CharData master, PetEntity pet, PetInfo data)
        {
            int mLvl = master.MLvl;
            const int sLvl = 1;
            int iLvl = Math.Clamp(CharStats.MainhandItemLevel(master) - 99, 0, 20);
            SetPetLevels(pet, (byte)(mLvl + iLvl + master.GetMod(Mod.WYVERN_LVL_BONUS)), sLvl);

            CalculatePetHPMP(pet);
            SetPetBaseStats(pet, data, sLvl);
            pet.MainDelay = pet.BaseDelay = data.CmbDelay;
            // Main Level / 2 is the BASE_DAMAGE_MULTIPLIER the wyvern script sets
            pet.MainDamage = (ushort)mLvl;

            CallPetGlobal("xi.pets.wyvern.onPetStatCalculate", master, pet);

            var stats = MobStatsFor();
            pet.AddModifier(Mod.DEF, (short)MobStats.GetBaseDefEva(pet, data.DefRank));
            pet.AddModifier(Mod.EVA, (short)MobStats.GetBaseDefEva(pet, stats.JobSkillRankToBaseEvaRank(pet.MJob, pet.SJob)));
            pet.AddModifier(Mod.ATT, (short)stats.GetBaseSkill(pet, data.AttRank));
            pet.AddModifier(Mod.RATT, (short)stats.GetBaseSkill(pet, data.AttRank));
            pet.AddModifier(Mod.ACC, (short)stats.GetBaseSkill(pet, data.AccRank));
            pet.AddModifier(Mod.RACC, (short)stats.GetBaseSkill(pet, data.AccRank));

            int jpValue = JobPointUtil.Value(master, JpWyvernMaxHpBonus);
            if (jpValue > 0) pet.AddModifier(Mod.HP, (short)(jpValue * 10));
            if (master.MJob == MobStats.JobDrg)
            {
                pet.AddModifier(Mod.ACC, master.GetMod(Mod.PET_ACC_EVA));
                pet.AddModifier(Mod.EVA, master.GetMod(Mod.PET_ACC_EVA));
                pet.AddModifier(Mod.MACC, master.GetMod(Mod.PET_MACC_MEVA));
                pet.AddModifier(Mod.MEVA, master.GetMod(Mod.PET_MACC_MEVA));
            }

            FinalizePetStatistics(master, pet);
        }

        // petutils::CalculateJugPetStats: the jug's cap (with Beast Affinity), no higher than the master's level or its
        // main hand's item level, 0-2 levels lower at random (less with JUG_LEVEL_RANGE)
        private void CalculateJugPetStats(CharData master, PetEntity pet, PetInfo data)
        {
            byte highestLvl = data.MaxLevel;
            if (master.Merits != null) highestLvl = (byte)(highestLvl + master.Merits.Value(master, MeritBeastAffinity));
            int capLevel = Math.Max(master.MLvl, CharStats.MainhandItemLevel(master));
            highestLvl = (byte)Math.Min(highestLvl, capLevel);
            highestLvl = (byte)(highestLvl - MobRng.Next(3 - Math.Clamp((int)master.GetMod(Mod.JUG_LEVEL_RANGE), 0, 2)));
            SetPetLevels(pet, Math.Min(pet.SpawnLevel, highestLvl), null);
            LoadJugStats(pet, data);

            CallPetGlobal("xi.pets.jug.onPetStatCalculate", master, pet);

            FinalizePetStatistics(master, pet);
        }

        // petutils::GetJugBase: a jug pet's DEF / EVA / ATT / ACC by rank
        private static ushort GetJugBase(PetEntity pet, int rank)
        {
            int lvl = pet.MLvl;
            if (lvl > 50)
            {
                return rank switch
                {
                    1 => (ushort)(153 + (lvl - 50) * 5.0f),
                    2 => (ushort)(147 + (lvl - 50) * 4.9f),
                    3 => (ushort)(136 + (lvl - 50) * 4.8f),
                    4 => (ushort)(126 + (lvl - 50) * 4.7f),
                    5 => (ushort)(116 + (lvl - 50) * 4.5f),
                    6 => (ushort)(106 + (lvl - 50) * 4.4f),
                    7 => (ushort)(96 + (lvl - 50) * 4.3f),
                    _ => 0,
                };
            }
            return rank switch
            {
                1 => (ushort)(6 + (lvl - 1) * 3.0f),
                2 => (ushort)(5 + (lvl - 1) * 2.9f),
                3 => (ushort)(5 + (lvl - 1) * 2.8f),
                4 => (ushort)(4 + (lvl - 1) * 2.7f),
                5 => (ushort)(4 + (lvl - 1) * 2.5f),
                6 => (ushort)(3 + (lvl - 1) * 2.4f),
                7 => (ushort)(3 + (lvl - 1) * 2.3f),
                _ => 0,
            };
        }

        // petutils::LoadJugStats: monster formulas without a sub job. Pet_t's HP and MP scales are never filled in
        // (LoadPetList leaves them 0), so the base HP and MP come out 0 as in the C++.
        private void LoadJugStats(PetEntity pet, PetInfo data)
        {
            int mLvl = pet.MLvl;
            double growth = mLvl > 75 ? 1.22 : mLvl > 65 ? 1.20 : mLvl > 55 ? 1.18 : mLvl > 50 ? 1.16 : mLvl > 45 ? 1.12 :
                            mLvl > 35 ? 1.09 : mLvl > 25 ? 1.07 : 1.0;
            pet.BaseMaxHp = (short)(17.0 * Math.Pow(mLvl, (float)growth) * data.HpScale);
            switch (pet.MJob)
            {
                case MobStats.JobPld:
                case MobStats.JobWhm:
                case MobStats.JobBlm:
                case MobStats.JobRdm:
                case MobStats.JobDrk:
                case MobStats.JobBlu:
                case MobStats.JobSch:
                    pet.BaseMaxMp = (short)(15.2 * Math.Pow(mLvl, 1.1075) * data.MpScale);
                    break;
            }

            pet.BaseSpeed = data.Speed;
            pet.AnimationSpeed = data.Speed;
            MobStats.UpdateSpeed(pet);
            CharStats.UpdateHealth(pet);
            pet.Tp = 0;
            pet.Hp = pet.MaxHp;
            pet.Mp = pet.MaxMp;

            pet.SetModifier(Mod.DEF, (short)GetJugBase(pet, data.DefRank));
            pet.SetModifier(Mod.EVA, (short)GetJugBase(pet, data.EvaRank));
            pet.SetModifier(Mod.ATT, (short)GetJugBase(pet, data.AttRank));
            pet.SetModifier(Mod.ACC, (short)GetJugBase(pet, data.AccRank));

            pet.SetMobMod(MobMod.DamageOffset, 2);
            pet.SetMobMod(MobMod.RangedDamageOffset, 2);
            pet.MainDamage = pet.RangedDamage = (ushort)mLvl;
            pet.WeaponSkill = data.CmbSkill;
            pet.MainDelay = pet.BaseDelay = data.CmbDelay;
            pet.RangedBaseDelay = 360;

            SetPetBaseStats(pet, data, pet.SLvl);
        }

        private const int JpBolsterEffect = 0x2A0 + 0x00;

        // petutils::CalculateLuopanStats: the master's level, 250 HP per 15 levels (3% more per Bolster job point under
        // Bolster), the flags for its size, and no speed: it sits where it was placed
        private void CalculateLuopanStats(CharData master, PetEntity pet)
        {
            SetPetLevels(pet, master.MLvl, null);
            int maxHp = 250 * pet.MLvl / 15;
            if (master.StatusEffects.HasStatusEffect(EffectId.Bolster))
                maxHp += (int)Math.Floor(maxHp * (0.03 * JobPointUtil.Value(master, JpBolsterEffect)));
            pet.BaseMaxHp = (short)maxHp;
            pet.Hp = maxHp;
            pet.EntityFlags = 0x0000008B;
            pet.BaseSpeed = 0;
            MobStats.UpdateSpeed(pet);
            FinalizePetStatistics(master, pet);
        }

        // petutils::FinalizePetStatistics: parry and guard skills, C-rank magic evasion, the master's pet mods, full HP
        // and MP, and Stout Servant's damage taken
        private void FinalizePetStatistics(CharData master, PetEntity pet)
        {
            var stats = MobStatsFor();
            if (pet.GetMobMod(MobMod.CanParry) > 0) pet.WorkingSkills[PetSkillParry] = stats.GetBaseSkill(pet, pet.GetMobMod(MobMod.CanParry));
            if ((pet.MJob == MobStats.JobMnk || pet.MJob == MobStats.JobPup) && pet.GetMobMod(MobMod.CannotGuard) == 0)
                pet.WorkingSkills[PetSkillGuard] = stats.GetBaseSkill(pet, 3);
            pet.SetModifier(Mod.MEVA, (short)(loader_.Stats.Growth.GetMaxSkillByRank(7, Math.Min((int)pet.MLvl, 99)) + pet.GetMod(Mod.MEVA)));
            pet.Tp = 0;
            ApplyPetModifiers(master, pet);
            CharStats.UpdateHealth(pet);
            pet.Hp = pet.MaxHp;
            pet.Mp = pet.MaxMp;

            if (master.TraitList.Find(t => t.Id == TraitStoutServant) is Trait stout)
                pet.AddModifier(Mod.DMG, (short)-(stout.Value * 100));
        }

        // CBattleEntity::applyPetModifiers / removePetModifiers: the master's pet mods of the pet's type
        private static void ApplyPetModifiers(BattleEntity master, PetEntity pet, int sign = 1)
        {
            foreach (var ((type, mod), value) in master.PetMods)
            {
                if (!CheckPetModType(pet, type)) continue;
                pet.AddModifier(mod, (short)(sign * value));
                CharStats.UpdateHealth(pet);
            }
        }

        // luautils::callGlobal(path, master, pet)
        private void CallPetGlobal(string path, BattleEntity master, PetEntity pet)
        {
            if (lua_ == null) return;
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (!lua_.IsFunction(-1)) return;
                lua_.PushObject(master);
                lua_.PushObject(pet);
                lua_.Call(2, 0);
            }
            catch (LuaException e)
            {
                ScriptError(path, e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // ---- perpetuation (CStatusEffectContainer::TickRegen) --------------------------------------------------------

        private const ushort EffectAstralFlow = 55, EffectAvatarsFavor = 431;
        private static readonly Mod[] PerpetuationAffinity =
        {
            Mod.FIRE_AFFINITY_PERP, Mod.ICE_AFFINITY_PERP, Mod.WIND_AFFINITY_PERP, Mod.EARTH_AFFINITY_PERP,
            Mod.THUNDER_AFFINITY_PERP, Mod.WATER_AFFINITY_PERP, Mod.LIGHT_AFFINITY_PERP, Mod.DARK_AFFINITY_PERP,
        };
        // the single weather of each element (its double is one more)
        private static readonly ushort[] PerpetuationWeather =
            { WeatherHotSpell, WeatherSnow, WeatherWind, WeatherDustStorm, WeatherThunder, WeatherRain, WeatherAuroras, WeatherGloom };

        // battleutils::GetDayElement: firesday .. darksday
        private byte DayElement()
        {
            byte[] dayElement = { 1, 4, 6, 3, 2, 5, 7, 8 };
            uint day = VanaTime.GetWeekDay(VanaTicksNow());
            return day < dayElement.Length ? dayElement[day] : (byte)0;
        }

        // the MP a tick costs: the base (AVATAR_PERPETUATION) halved by the Carbuncle / day / weather gear, less the
        // reductions and the element's affinity, the day's and weather's reductions on a match, x1.2 under Avatar's
        // Favor (floored); at least 1, none under Astral Flow
        private int PerpetuationCost(CharData c)
        {
            int cost = c.GetMod(Mod.AVATAR_PERPETUATION);
            if (c.PPet is PetEntity pet)
            {
                byte element = pet.Element;
                bool elementValid = element >= 1 && element <= 8;
                if (!elementValid)
                    log_($"CStatusEffectContainer::TickRegen() - Pet {pet.Name} (PetID {pet.PetId}) has invalid element {element} for avatar perpetuation. Check pet_list.sql.");
                int idx = elementValid ? element - 1 : 0;
                ushort weather = BattleWeather(c);
                bool dayMatch = elementValid && DayElement() == element;
                bool weatherMatch = elementValid && (weather == PerpetuationWeather[idx] || weather == PerpetuationWeather[idx] + 1);

                if (c.GetMod(Mod.HALF_PERPETUATION_CARBUNCLE) != 0 && pet.PetId == PetId.Carbuncle ||
                    c.GetMod(Mod.HALF_PERPETUATION_DAY) != 0 && dayMatch || c.GetMod(Mod.HALF_PERPETUATION_WEATHER) != 0 && weatherMatch)
                    cost = (short)(cost / 2);
                cost = (short)(cost - c.GetMod(Mod.PERPETUATION_REDUCTION));
                if (elementValid) cost = (short)(cost - c.GetMod(PerpetuationAffinity[idx]));
                if (dayMatch) cost = (short)(cost - c.GetMod(Mod.DAY_REDUCTION));
                if (weatherMatch) cost = (short)(cost - c.GetMod(Mod.WEATHER_REDUCTION));
                if (c.StatusEffects.HasStatusEffect(EffectAvatarsFavor) && PetId.IsAvatar(pet.PetId)) cost = (short)(cost * 1.2);
            }
            if (c.StatusEffects.HasStatusEffect(EffectAstralFlow)) cost = 0;
            else if (cost < 1) cost = 1;
            return cost;
        }

        // ---- spawning ----------------------------------------------------------------------------------------------

        // petutils::SpawnPet: LoadPet, then the pet joins its master's side and the zone, spawns and is set up with
        // its master
        private void SpawnPet(CharData master, int petId, bool spawningFromZone = false)
        {
            if (master.PPet != null)
            {
                log_($"Pet was not null for {master.Name}.");
                return;
            }
            LoadPet(master, petId);
            if (master.PPet is not PetEntity pet)
            {
                ResetPetZoningInfo(master);
                return;
            }

            pet.Allegiance = master.Allegiance;
            foreach (var e in master.StatusEffects.All)
                if (!e.Deleted && e.HasFlag(EffectFlag.Confrontation)) pet.StatusEffects.AddStatusEffect(e.Copy());
            pet.PMaster = master;
            if (master.PBattlefield != null) pet.PBattlefield = master.PBattlefield;
            // Don't play special spawn animation on zone in
            if (spawningFromZone) pet.SpawnAnimation = 0;
            InsertPet(pet);
            PetSpawn(pet);
            SetupPetWithMaster(master, pet);
            // apply stats from previous zone if this pet is being transferred
            if (spawningFromZone) LoadPetZoningInfo(pet);
        }

        // CPetEntity::Spawn: no stat calculation (LoadPet did it); a char's elemental gets its spell timing and
        // spells, a jug pet's time starts; then CBattleEntity::Spawn and OnMobSpawn. CAutomatonEntity::Spawn keeps
        // its local vars and entity data (its attachments' onEquip set them on the fresh entity).
        private void PetSpawn(PetEntity pet)
        {
            long now = NowMs();
            if (pet.PMaster is CharData && pet.Ecosystem == XI.Zone.Ecosystem.Elemental)
            {
                pet.DefaultMobMod(MobMod.MagicDelay, 12);
                pet.DefaultMobMod(MobMod.MagicCool, 48);
                ResolveMobSpells(pet);
            }
            if (pet.PetType == PetType.JugPet) pet.JugSpawnMs = now;
            pet.Animation = Anim.None;
            pet.HideName(false);
            pet.Status = pet.Allegiance == 0 ? NpcEntity.StatusUpdate : NpcEntity.StatusNormal;
            pet.UpdateMask |= EntityUpdatePacket.UpdateHp;
            if (pet is not AutomatonEntity)
            {
                pet.LocalVars.Clear();
                ResetEntityData(pet);
            }
            ClearActionQueues(pet);
            pet.OwnerId = 0;
            MobControllerReset(pet, now);
            OnMobSpawn(pet);
        }

        // petutils::SetupPetWithMaster: the pet's job traits, the master's status and pet sync, the latents that
        // look at pets, and the master's Debilitation, Omerta and Impairment
        private void SetupPetWithMaster(CharData master, PetEntity pet)
        {
            if (pet.PetType != PetType.Automaton) TraitAddMobTraits(pet);
            loader_.BuildAbilityTable(master);
            BuildPetAbilityTable(master, pet, pet.PetId);
            PushTo(master, CharStatus(master));
            PushTo(master, PetSyncPacket.Create(master));
            master.Latents.CheckLatentsPetType();
            LatentsForParty(master, m => m.Latents.CheckLatentsPartyAvatar());

            foreach (ushort id in new[] { EffectDebilitation, EffectOmerta, EffectImpairment })
            {
                if (master.StatusEffects.GetStatusEffect(id) is not StatusEffect e) continue;
                pet.StatusEffects.AddStatusEffect(new StatusEffect(id, id, e.Power, 0, e.DurationMs), EffectNotice.Silent);
            }
        }

        // CZoneEntities::InsertPET: a dynamic targid and id, the zone's pet list, the chars nearby, then no more special
        // spawn animation
        private void InsertPet(PetEntity pet)
        {
            var info = AreaOf(pet);
            info.AssignDynamicTargId(pet, NowMs());
            info.Pets.Add(pet);
            foreach (var (c, s) in charSessions_)
            {
                if (!SameArea(c, pet) || !s.InZone || !PetVisible(c, pet)) continue;
                if (c.SpawnPetList.Add(pet.Id)) UpdateEntityPacket(s, pet, EntityUpdate.Spawn, EntityUpdatePacket.UpdateAllMob);
            }
            pet.SpawnAnimation = 0;
        }

        // SpawnPETs' visible: up (normal or update), at eye level and within 50 yalms
        private static bool PetVisible(CharData c, PetEntity pet)
        {
            if (pet.Status != NpcEntity.StatusNormal && pet.Status != NpcEntity.StatusUpdate) return false;
            if (Math.Abs(pet.Y - c.Y - 0.5f) > 20.0f) return false;
            float dx = c.X - pet.X, dy = c.Y - pet.Y, dz = c.Z - pet.Z;
            return dx * dx + dy * dy + dz * dz <= EntityRenderDistance * EntityRenderDistance;
        }

        // CZoneEntities::SpawnPETs
        private void SpawnPets(MapSession s, CharData c)
        {
            var zone = AreaOf(c);
            var byId = new Dictionary<uint, PetEntity>();
            foreach (var pet in zone.Pets) byId[pet.Id] = pet;
            foreach (var id in new List<uint>(c.SpawnPetList))
            {
                if (byId.TryGetValue(id, out var pet) && PetVisible(c, pet)) continue;
                c.SpawnPetList.Remove(id);
                if (pet != null) UpdateEntityPacket(s, pet, EntityUpdate.Despawn, 0);
            }
            foreach (var pet in zone.Pets)
                if (PetVisible(c, pet) && c.SpawnPetList.Add(pet.Id)) UpdateEntityPacket(s, pet, EntityUpdate.Spawn, EntityUpdatePacket.UpdateAllMob);
        }

        // ---- leaving -----------------------------------------------------------------------------------------------

        // petutils::DetachPet for a char's CPetEntity: the pet dies unless dead already, an avatar's perpetuation
        // cost ends, the latents are checked again, the pet mods come off; the char's status, abilities and pet sync
        private void DetachPet(CharData master)
        {
            if (master.PPet is MobEntity charmed and not PetEntity)
            {
                DetachCharmedMob(master, charmed);
                return;
            }
            if (master.PPet is not PetEntity pet)
            {
                if (master.PPet == null) log_($"Pet is null for {master.Name}.");
                return;
            }
            if (!PetIsDead(pet)) PetDie(pet, NowMs());
            // PetDie detached it already
            if (master.PPet != pet) return;

            if (pet.PetType == PetType.Avatar) master.SetModifier(Mod.AVATAR_PERPETUATION, 0);
            master.Latents.CheckLatentsPetType();
            LatentsForParty(master, m => m.Latents.CheckLatentsPartyAvatar());
            pet.PMaster = null;
            ApplyPetModifiers(master, pet, -1);
            BuildPetAbilityTable(master, pet, 0); // blank the pet commands

            // the ability table is rebuilt while the pet is still the master's (as in C++)
            loader_.BuildAbilityTable(master);
            master.PPet = null;
            PushTo(master, CharStatus(master));
            PushTo(master, LoginPackets.CommandData(master));
            PushTo(master, PetSyncPacket.Create(master));
        }

        // petutils::DespawnPet
        private void DespawnPet(CharData master)
        {
            if (master.PPet == null)
            {
                log_($"Pet is null for {master.Name}.");
                return;
            }
            DetachPet(master);
        }

        // CBattleEntity::isDead for a pet: no HP, gone, or dying (the death or despawn state)
        private static bool PetIsDead(PetEntity pet) => pet.Hp <= 0 || pet.Status == NpcEntity.StatusDisappear || pet.DeathAtMs != 0 || pet.FadeAtMs != 0;

        // CPetEntity::Die: the death state (2.5 s, then the despawn's 3 s), or an instant despawn while the master is
        // zoning with it; OnMobDeath without a killer, the DEATH listeners, and a char master lets it go
        private void PetDie(PetEntity pet, long now)
        {
            AbortMagic(pet, now);
            AbortMobSkill(pet, now);
            bool zoning = pet.Hp > 0 && pet.PMaster is CharData zoningMaster && zoningMaster.PetZoning.RespawnPet;
            pet.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Death, EffectNotice.Silent);
            if (!zoning) pet.Animation = Anim.Death;
            pet.UpdateMask |= EntityUpdatePacket.UpdateHp;
            pet.PathFind?.Clear();
            pet.DeathAtMs = now;
            // master is zoning, don't go to death state, instead despawn instantly
            if (zoning) pet.FadeAtMs = now;
            OnMobDeath(pet, null);
            TriggerListener(pet, "DEATH", pet);
            pet.BattleTarget = null;
            pet.Engaged = false;
            // CAutomatonEntity::Die: the master's maneuvers go with it
            if (pet is AutomatonEntity && pet.PMaster != null) RemoveAllInRange(pet.PMaster, EffectId.FireManeuver, EffectId.DarkManeuver);
            if (pet.PMaster is CharData master && master.PPet == pet) DetachPet(master);
        }

        // CZone::DecreaseZoneCounter: the char's pet disappears with it (an avatar's perpetuation cost ends) and the
        // chars that had it spawned see it go
        private void RemoveCharPet(CharData c)
        {
            if (c.PPet is not PetEntity pet) return;
            BuildPetAbilityTable(c, pet, 0); // blank the pet commands
            pet.Status = NpcEntity.StatusDisappear;
            if (pet.PetType == PetType.Avatar) c.SetModifier(Mod.AVATAR_PERPETUATION, 0);
            if (pet.Engaged) MobDisengage(pet, NowMs());
            foreach (var (o, os) in charSessions_)
                if (o.SpawnPetList.Remove(pet.Id)) UpdateEntityPacket(os, pet, EntityUpdate.Despawn, 0);
            c.PPet = null;
        }

        // ---- the tick ----------------------------------------------------------------------------------------------

        // CZoneEntities::petTick and CPetEntity::PostTick for each zone's pets: a gone pet leaves the mobs' enmity, the
        // chars' spawn lists and the zone (its targid freed 60 s later); the others run their controller every 400 ms,
        // their death and despawn timers, and send their updates at most every 250 ms with the master's pet sync
        private void TickPets()
        {
            long now = NowMs();
            foreach (var (info, _, _) in AllAreas())
            {
                foreach (var pet in info.Pets.ToArray())
                {
                    if (pet.Status == NpcEntity.StatusDisappear)
                    {
                        foreach (var m in info.Mobs) m.Enmity.Clear(pet.Id);
                        foreach (var (c, _) in charSessions_) c.SpawnPetList.Remove(pet.Id);
                        info.Pets.Remove(pet);
                        info.ReleaseDynamicTargId(pet.TargId, now);
                        continue;
                    }

                    if (pet.DeathAtMs != 0 && pet.FadeAtMs == 0 &&
                        now > pet.DeathAtMs + PetDeathMs - pet.GetMod(Mod.DESPAWN_TIME_REDUCTION) * 1000L)
                        pet.FadeAtMs = now + PetDespawnMs;
                    if (pet.FadeAtMs != 0 && now >= pet.FadeAtMs)
                    {
                        pet.FadeAtMs = 0;
                        PetFadeOut(pet);
                        continue;
                    }

                    // CZoneEntities::ZoneServer (pet): expired spell and ability recasts go
                    pet.Recasts?.Check();

                    if (pet.DeathAtMs == 0 && pet.FadeAtMs == 0 && now >= pet.NextAiTickMs)
                    {
                        pet.NextAiTickMs = now + MobAiTickMs;
                        PetControllerTick(pet, now);
                        // the state on top: a skill, a spell, else the attack state's swing
                        if (pet.SkillState != null) TickMobSkill(pet, now);
                        else if (pet.MagicState != null) TickMagic(pet, now);
                        else if (TickMobAbility(pet, now)) { }
                        else if (pet.Engaged && pet.DeathAtMs == 0) UpdatePetAttackState(pet, now);
                    }

                    // CBattleEntity::PostTick: out of HP and not yet dying (a jug pet without HP mods dies at once, as
                    // in C++)
                    if (pet.Hp <= 0 && pet.DeathAtMs == 0 && pet.FadeAtMs == 0) PetDie(pet, now);

                    if (pet.UpdateMask != 0 && pet.Status != NpcEntity.StatusDisappear && now > pet.NextUpdateMs)
                    {
                        pet.NextUpdateMs = now + 250;
                        foreach (var (c, s) in charSessions_)
                            if (InArea(c, info) && c.SpawnPetList.Contains(pet.Id)) UpdateEntityPacket(s, pet, EntityUpdate.Update, pet.UpdateMask);
                        if (pet.PMaster is CharData master && master.PPet == pet)
                        {
                            PushTo(master, PetSyncPacket.Create(master));
                            // CAutomatonEntity::PostTick: the master's PUP job packets carry its HP and MP
                            if (pet is AutomatonEntity && SessionOf(master) is { } ms) SendExtendedJobPackets(ms, master);
                        }
                        pet.UpdateMask = 0;
                    }
                }
                info.EraseStaleDynamicTargIds(now);
            }
        }

        // CPetEntity::FadeOut: gone, and the chars that had it spawned are told
        private void PetFadeOut(PetEntity pet)
        {
            pet.FadeOut();
            pet.UpdateMask = 0;
            foreach (var (c, s) in charSessions_)
                if (c.SpawnPetList.Remove(pet.Id)) UpdateEntityPacket(s, pet, EntityUpdate.Despawn, 0);
        }

        // CPetController::Tick for a char's pet or charmed mob: a charmed mob whose charm time is up and a jug pet whose
        // time is up leave; then the mob controller's tick
        private void PetControllerTick(MobEntity pet, long now)
        {
            if (pet.IsCharmed && pet.PMaster is CharData charmMaster && now > pet.CharmTimeMs)
            {
                DespawnPet(charmMaster);
                return;
            }
            if (pet is PetEntity { PetType: PetType.JugPet } jug && jug.IsAlive && jug.PMaster is CharData jugMaster &&
                now > jug.JugSpawnMs + jug.JugDurationMs)
            {
                DespawnPet(jugMaster);
                return;
            }
            // CMobController::Tick: the combat tick, else (not dead) an idle buff before the roam
            if (!pet.IsAlive) return;
            if (pet.Engaged && pet is AutomatonEntity automaton) AutomatonCombatTick(automaton, now);
            else if (pet.Engaged) PetCombatTick(pet, now);
            else if (!pet.IsDead)
            {
                if (pet.SpellContainer.HasSpells && !(pet.PathFind?.IsPatrolling ?? false) && PetDoBuffTick(pet, now)) return;
                PetRoamTick(pet, now);
            }
        }

        // CPetController::DoBuffTick: only the Light Spirit casts out of combat (its buffs, through TryCastSpell)
        private bool PetDoBuffTick(MobEntity pet, long now)
        {
            if (pet is not PetEntity { PetId: PetId.LightSpirit }) return false;
            if (pet.MagicState != null) return true;
            if (!IsSpellReady(pet, 0, 0, now) || !pet.SpellContainer.HasBuffSpells()) return false;
            return TryCastSpell(pet, now);
        }

        // ---- pet combat --------------------------------------------------------------------------------------------

        // petutils::AttackTarget -> CAIContainer::Engage for a char's pet: CPetEntity::CanAttack refuses a mob the
        // master does not own; then CMobController::Engage
        private void PetEngage(MobEntity pet, MobEntity target, long now)
        {
            if (!target.IsAlive || target.IsDead || !target.IsSpawned) return;
            if (pet.PMaster is CharData master && !IsMobOwner(master, target))
            {
                if (pet.Engaged) MobDisengage(pet, now);
                return;
            }
            if (pet.Engaged && pet.BattleTarget == target) return;
            MobEngage(pet, target, now);
        }

        // CMobController::DoCombatTick with CPetController's overrides: no enmity of its own (it fights its master's
        // target), TryDeaggro lets go of a target that is down, gone or in another zone, no linking and no weapon
        // skills (no special skill or TP move); the claim check of CPetEntity::CanAttack; then a spell when the magic
        // cool is up, a ranged attack, else the chase.
        private void PetCombatTick(MobEntity pet, long now)
        {
            var target = pet.BattleTarget;
            FaceTarget(pet, target);
            if (PetTryDeaggro(pet, target))
            {
                AbortMagic(pet, now);
                MobDisengage(pet, now);
                return;
            }
            if (pet.PMaster is CharData master && target is MobEntity mob && !IsMobOwner(master, mob))
            {
                MobDisengage(pet, now);
                return;
            }
            TriggerListener(pet, "COMBAT_TICK", pet);
            OnMobFight(pet, target);
            if (!pet.Engaged || pet.BattleTarget == null || pet.InactiveState != null || !CanChangeState(pet)) return;
            if (MobRunAwayFollow(pet, now)) return;
            if (IsSpecialSkillReady(pet, Distance(pet, pet.BattleTarget), now) && TrySpecialSkill(pet, now)) return;
            float meleeRange = MeleeRange(pet, pet.BattleTarget);
            if (IsSpellReady(pet, Distance(pet, pet.BattleTarget), meleeRange, now) && TryCastSpell(pet, now)) return;
            if (now >= pet.LastMobSkillMs && ShouldUseTpMove(pet) && MobSkillFromList(pet, 0))
            {
                pet.TpThreshold = MobRng.Next(1000, 3001);
                return;
            }
            if (MobTryRangedAttack(pet, now)) return;
            if (!MobFollowScriptedPath(pet, now)) MobMove(pet, pet.BattleTarget, now);
        }

        // CPetController::TryDeaggro
        private static bool PetTryDeaggro(MobEntity pet, BattleEntity target)
        {
            if (target == null) return true;
            bool targetDead = target is MobEntity m ? !m.IsAlive || m.IsDead || !m.IsSpawned : target.Hp <= 0;
            return targetDead || target.StatusEffects.HasStatusEffect(EffectId.Mounted) || !SameArea(target, pet) ||
                   pet.BattleId != target.BattleId;
        }

        // CAttackState::Update for a pet: a swing whenever one is due and the target is in reach
        private void UpdatePetAttackState(MobEntity pet, long now)
        {
            if (pet.BattleTarget is not MobEntity target || !target.IsAlive || target.IsDead || now < pet.SwingAtMs) return;
            if (Distance(pet, target) <= MeleeRange(pet, target) && pet.AutoAttackEnabled)
            {
                pet.SwingAtMs += pet.SwingDelayMs;
                var action = MobAttack(pet, target, now);
                if (action != null) PushMobInRange(pet, Battle2Packets.Battle2(action));
            }
            if (pet.SwingAtMs < now) pet.SwingAtMs = now;
        }

        // battleutils::TakePhysicalDamage for a char's pet (or charmed mob) hitting a mob: the damage, the claim for its
        // master, the mob's enmity toward the pet, the spell interrupt and both sides' TP
        private int MobTakePhysicalDamageFromPet(MobEntity pet, MobEntity defender, int damage, int slot, long now, bool isBlocked,
            bool giveTpToAttacker, bool isCounter)
        {
            giveTpToAttacker = giveTpToAttacker && !pet.StatusEffects.HasStatusEffect(EffectMeikyoShisui);
            int dmg = MeleeFor().MeleeDamageTaken(pet, defender, damage, slot, isBlocked, isCounter, tp => AddMobTp(defender, (short)tp),
                addMp: mp => BattleAddMp(defender, mp), giveTpToAttacker: giveTpToAttacker);
            dmg = PhysicalTakeDamage(defender, pet, dmg, slot, isCounter);
            if (defender.Allegiance != CharData.AllegiancePlayer) ClaimMob(defender, pet, now);
            if (dmg > 0)
            {
                defender.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Damage);
                BindBreakCheck(defender);
                TryHitInterrupt(defender, pet);
                defender.Enmity.UpdateEnmityFromDamage(pet, dmg);
                if (giveTpToAttacker) AddMobTp(pet, (short)Melee.SingleMeleeHitTpReturn(pet));
                AddMobTp(defender, (short)Melee.TpGainOnPhysicalDamage(pet, defender, dmg, Melee.BaseDelay(pet)));
            }
            else defender.Enmity.UpdateEnmityFromDamage(pet, 0);
            return dmg;
        }

        // battleutils::TakePhysicalDamage for a mob hitting a char's pet (or charmed mob): the pet's HP, its
        // damage-broken effects, a charmed mob's enmity toward the mob (a hit or not), the mob's enmity toward it (lost
        // from normal attacks), the spell interrupt and both sides' TP
        private int PetTakePhysicalDamage(MobEntity attacker, MobEntity pet, int damage, int slot, long now, bool isBlocked,
            bool giveTpToAttacker, bool isCounter)
        {
            giveTpToAttacker = giveTpToAttacker && !attacker.StatusEffects.HasStatusEffect(EffectMeikyoShisui);
            int dmg = MeleeFor().MeleeDamageTaken(attacker, pet, damage, slot, isBlocked, isCounter, tp => AddMobTp(pet, (short)tp),
                addMp: mp => BattleAddMp(pet, mp), giveTpToAttacker: giveTpToAttacker);
            dmg = PhysicalTakeDamage(pet, attacker, dmg, slot, isCounter);
            if (dmg > 0)
            {
                pet.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Damage);
                BindBreakCheck(pet);
                // a charmed mob is a mob: its own list takes the attacker
                if (pet is not PetEntity and not TrustEntity) pet.Enmity.UpdateEnmityFromDamage(attacker, dmg);
                attacker.Enmity.UpdateEnmityFromAttack(pet, dmg);
                TryHitInterrupt(pet, attacker);
                if (giveTpToAttacker) AddMobTp(attacker, (short)Melee.SingleMeleeHitTpReturn(attacker));
                AddMobTp(pet, (short)Melee.TpGainOnPhysicalDamage(attacker, pet, dmg, Melee.BaseDelay(attacker)));
            }
            else if (pet is not PetEntity and not TrustEntity) pet.Enmity.UpdateEnmityFromDamage(attacker, 0);
            return dmg;
        }

        // CPetController::DoRoamTick: a pet (not a charmed mob) whose master is gone or dead dies; a wyvern rests with
        // its master, a resting BST pet (a jug pet or a charmed mob) and the immobile pets stay; the others follow the
        // master once more than 2.1 yalms away, running, the path renewed when the master has moved 2 yalms from its end
        // (warped next to the master when no path reaches it)
        private void PetRoamTick(MobEntity pet, long now)
        {
            if ((pet.PMaster == null || pet.PMaster.Hp <= 0) && pet.IsAlive && pet is PetEntity dying)
            {
                PetDie(dying, now);
                return;
            }
            if (pet.MagicState != null || pet.SkillState != null || pet.StatusEffects.HasPreventActionEffect(false)) return;

            if (pet is PetEntity { PetType: PetType.Wyvern } && PetIsHealing(pet)) return;
            if (pet is PetEntity immobile && PetId.IsImmobile(immobile.PetId)) return;
            // a CMobEntity that has been charmed is a BST pet
            bool isBstPet = pet is PetEntity bst ? bst.IsBstPet : true;
            if (isBstPet && pet.StatusEffects.HasStatusEffect(EffectId.Healing)) return;

            var master = pet.PMaster;
            if (master == null) return;
            if (Distance(pet, master) <= PetRoamDistance) return;

            var path = PathOf(pet);
            if (!path.IsFollowingPath || DistanceTo(path.Destination, master) > 2.0f)
            {
                if (!path.PathAround(master.X, master.Y, master.Z, 2.0f, PathFlag.Run) &&
                    !path.PathInRange(master.X, master.Y, master.Z, 2.0f, PathFlag.Run))
                {
                    // If we got here, the pet isn't able to path to master
                    // But it cant, so maybe we teleported or dropped down a hole
                    path.WarpTo(master.X, master.Y, master.Z, master.Rotation, PetRoamDistance);
                }
            }
            path.FollowPath(now);
        }

        private static float DistanceTo((float X, float Y, float Z) p, BattleEntity e)
        {
            float dx = p.X - e.X, dy = p.Y - e.Y, dz = p.Z - e.Z;
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        // CPetController::PetIsHealing: the pet rests and gets up with its master
        private bool PetIsHealing(MobEntity pet)
        {
            bool masterHealing = pet.PMaster.Animation == Anim.Healing;
            bool petHealing = pet.Animation == Anim.Healing;
            if (masterHealing && !petHealing && !pet.StatusEffects.HasPreventActionEffect(false))
            {
                pet.Animation = Anim.Healing;
                pet.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.Healing, 0, 0, ChatCfg.HealingTickDelay * 1000L, 0));
                pet.UpdateMask |= EntityUpdatePacket.UpdateHp;
                return true;
            }
            if (!masterHealing && petHealing)
            {
                pet.Animation = Anim.None;
                pet.StatusEffects.DelStatusEffect(EffectId.Healing);
                pet.UpdateMask |= EntityUpdatePacket.UpdateHp;
                return false;
            }
            return masterHealing;
        }
    }
}
