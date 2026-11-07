using System;

namespace XI.Zone
{
    // mobutils: a mob's attributes applied (ApplySpecies), its one-time mob mods (InitializeMob) and the stats it gets
    // on each spawn (CalculateMobStats). The settings multipliers (map.MOB_HP_MULTIPLIER and the NM / MP / stat ones)
    // are their defaults of 1, and map.INCLUDE_MOB_SJ is off, so a mob's sub level is its main level. The traits come
    // from the zone host's AddTraits hook. A mob pet has 30% of the HP and an avatar its species' skill list
    // (SetupPetSkills). SetupRangedAttack's controller flag is MobEntity.RangedAttackEnabled; the gil purse is the zone
    // host's (GilPurse).
    public sealed class MobStats
    {
        public const byte JobWar = 1, JobMnk = 2, JobWhm = 3, JobBlm = 4, JobRdm = 5, JobThf = 6, JobPld = 7, JobDrk = 8,
            JobBst = 9, JobBrd = 10, JobRng = 11, JobNin = 13, JobDrg = 14, JobSmn = 15, JobBlu = 16, JobPup = 18, JobSch = 20, JobGeo = 21,
            JobRun = 22;
        private const int SkillGuard = 28, SkillParry = 31, SkillDivineMagic = 32, SkillBlueMagic = 43;

        private readonly Grades grades_;
        private readonly GrowthTables growth_;

        // battleutils::AddTraits for the main and the sub job, which the zone host owns (the traits table and the
        // xi.traits.canApplyTrait filter)
        public Action<MobEntity> AddTraits;
        // mobutils' spell-list half, run after CalculateMobStats: the SpellList mob-mod override and GetAvailableSpells
        public Action<MobEntity> ResolveSpells;
        // CMobEntity::CanStealGil and ResetGilPurse, which the zone host owns (GetRandomGil on the mob RNG)
        public Action<MobEntity> GilPurse;
        // loc.zone->navMesh(): a region spawn's point is checked against it
        public Func<ushort, DetourNavMesh> NavMeshOf;

        public MobStats(Grades grades, GrowthTables growth)
        {
            grades_ = grades;
            growth_ = growth;
        }

        // ApplySpecies(PMob, attributes): the merged ecosystem -> family -> species -> template -> spawn chain
        public static void ApplySpecies(MobEntity m, SpeciesInfo species, MobAttributes a)
        {
            m.Ecosystem = species.Ecosystem;
            m.Family = species.Family;
            m.Element = a.Element ?? 0;
            m.BaseSpeed = a.SpeedOrDefault;
            m.AnimationSpeed = a.AnimationSpeed ?? a.SpeedOrDefault;
            UpdateSpeed(m);
            m.StrRank = a.StrRank; m.DexRank = a.DexRank; m.VitRank = a.VitRank; m.AgiRank = a.AgiRank;
            m.IntRank = a.IntRank; m.MndRank = a.MndRank; m.ChrRank = a.ChrRank;
            m.DefRank = a.DefRank; m.EvaRank = a.EvaRank; m.AttRank = a.AttRank; m.AccRank = a.AccRank;
            m.SetMobMod(MobMod.Detection, (short)(a.Detects ?? 0));
            bool special = (m.Type & (MobType.Event | MobType.Fished | MobType.Battlefield | MobType.Notorious)) != 0;
            m.SetMobMod(MobMod.Charmable, (short)((a.Charmable ?? false) && !special ? 1 : 0));
            foreach (var (mod, value) in a.Resists) m.SetModifier(mod, value);
            if (a.Behavior is ushort behavior) m.Behavior = behavior;
            if (a.Immune is uint immune) m.Immunity = immune;
            if (a.MainJob is byte mJob) m.MJob = mJob;
            if (a.SubJob is byte sJob) m.SJob = sJob;
            m.Aggro = a.Aggressive ?? false;
            m.Link = a.Links ?? false;
            m.TrueDetection = a.TrueDetection ?? false;
        }

        // CBattleEntity::UpdateSpeed for a mob (no mount, no gear factor); every step truncates to int16. With run
        // (PATHFLAG_RUN) a moving mob's speed takes MOB_RUN_SPEED_MULTIPLIER, or its RunSpeedMult mobmod, cut to
        // 0.48 of it under a weight penalty and never below 1; the result is clamped to 0..255. Returns the speed
        public static byte UpdateSpeed(MobEntity m, bool run = false, float runSpeedMultiplier = 2.5f)
        {
            short output;
            short overrideSpeed = m.GetMod(Mod.MOVE_SPEED_OVERRIDE);
            if (m.BaseSpeed == 0 || overrideSpeed < 0) output = 0;
            else if (overrideSpeed > 0) output = overrideSpeed;
            else
            {
                output = (short)(m.BaseSpeed + (sbyte)m.GetMod(Mod.MOVE_SPEED_STACKABLE));
                output = (short)(output * Math.Clamp(1.0f - m.GetMod(Mod.MOVE_SPEED_WEIGHT_PENALTY) / 100.0f, 0.1f, 1.0f));
                output = (short)(output * Math.Clamp(1.0f + m.GetMod(Mod.MOVE_SPEED_FLEE) / 10000.0f, 1.0f, 2.0f));
                output = (short)(output * ((99 + m.GetMod(Mod.MOVE_SPEED_CHEER)) / 99.0f));
                output = (short)(output + (byte)m.GetMod(Mod.MOVE_SPEED_BOLTERS_ROLL));
                if (output > 0) output = (short)(output + Math.Clamp(m.GetMod(Mod.MOVE_SPEED_QUICKENING) + m.GetMod(Mod.MOVE_SPEED_MAZURKA), 0, 10));
                if (run && output > 0 && runSpeedMultiplier > 1.0f)
                {
                    float multiplier = runSpeedMultiplier;
                    if (m.GetMobMod(MobMod.RunSpeedMult) > 0) multiplier = m.GetMobMod(MobMod.RunSpeedMult) / 100.0f;
                    if (m.GetMod(Mod.MOVE_SPEED_WEIGHT_PENALTY) > 0) multiplier *= 0.48f;
                    multiplier = Math.Max(multiplier, 1.0f);
                    output = (short)(output * multiplier);
                }
            }
            m.Speed = (byte)Math.Clamp(output, (short)0, (short)255);
            return m.Speed;
        }

        // InitializeMob, with battleutils::addEcosystemKillerEffects
        public static void InitializeMob(MobEntity m)
        {
            m.DefaultMobMod(MobMod.SkillList, (short)m.MobSkillList);
            m.DefaultMobMod(MobMod.LinkRadius, 10);
            m.DefaultMobMod(MobMod.SightRange, 15);
            m.DefaultMobMod(MobMod.SoundRange, 8);
            m.DefaultMobMod(MobMod.MagicRange, 20);
            AddEcosystemKillerEffects(m);
        }

        // battleutils::addEcosystemKillerEffects: +5 killer against the ecosystem this one's is strong against
        public static void AddEcosystemKillerEffects(MobEntity m)
        {
            Mod killer = m.Ecosystem switch
            {
                Zone.Ecosystem.Amorph => Mod.BIRD_KILLER,
                Zone.Ecosystem.Aquan => Mod.AMORPH_KILLER,
                Zone.Ecosystem.Arcana => Mod.UNDEAD_KILLER,
                Zone.Ecosystem.Beast => Mod.LIZARD_KILLER,
                Zone.Ecosystem.Bird => Mod.AQUAN_KILLER,
                Zone.Ecosystem.Demon => Mod.DRAGON_KILLER,
                Zone.Ecosystem.Dragon => Mod.DEMON_KILLER,
                Zone.Ecosystem.Lizard => Mod.VERMIN_KILLER,
                Zone.Ecosystem.Luminion => Mod.LUMINIAN_KILLER,
                Zone.Ecosystem.Luminian => Mod.LUMINION_KILLER,
                Zone.Ecosystem.Plantoid => Mod.BEAST_KILLER,
                Zone.Ecosystem.Undead => Mod.ARCANA_KILLER,
                Zone.Ecosystem.Vermin => Mod.PLANTOID_KILLER,
                _ => 0,
            };
            m.AddModifier(killer, 5);
        }

        // CalculateMobStats. `instanced` is the zone's ZoneType::Instanced bit.
        public void CalculateMobStats(MobEntity m, bool recover, bool instanced)
        {
            // the effects go only on a full recovery
            if (recover) m.StatusEffects.KillAllStatusEffect();
            m.RestoreModifiers();
            m.RestoreMobModifiers();
            byte mJob = m.MJob, sJob = m.SJob, mLvl = m.MLvl, sLvl = m.SLvl;

            if (recover)
            {
                if (m.HpModifier == 0)
                {
                    byte mGrade = grades_.GetJobGrade(mJob, 0), sGrade = grades_.GetJobGrade(sJob, 0);
                    uint hp = CalculateBaseMobHP(mLvl, MobHpScale[mGrade, 0], MobHpScale[mGrade, 1], MobHpScale[mGrade, 2]) +
                              CalculateSubjobHP(mLvl, MobHpScale[sGrade, 1], MobHpScale[sGrade, 2]);
                    // pets are 30% of base mob HP
                    if (m.PMaster != null) hp = (uint)(hp * 0.30f);
                    m.BaseMaxHp = (short)hp;
                }
                else m.BaseMaxHp = (int)m.HpModifier;

                bool hasMp = HasMp(mJob) || HasMp(sJob) || m.GetMobMod(MobMod.MpBase) != 0;
                if (hasMp)
                {
                    float scale = m.MpScale;
                    if (m.GetMobMod(MobMod.MpBase) != 0) scale = m.GetMobMod(MobMod.MpBase) / 100.0f;
                    m.BaseMaxMp = m.MpModifier == 0 ? (short)(18.2 * Math.Pow(mLvl, 1.1075) * scale) + 10 : (int)m.MpModifier;
                }
            }

            m.MainDamage = GetBaseWeaponDamage(m);
            m.RangedDamage = m.MainDamage;
            if (mJob == JobMnk) m.MainDelay = m.BaseDelay;

            byte[] familyRanks = { m.StrRank, m.DexRank, m.VitRank, m.AgiRank, m.IntRank, m.MndRank, m.ChrRank };
            bool subJobZone = CheckSubJobZone(m.Zone) && sLvl < 50;
            for (int k = 0; k < 7; k++)
            {
                byte sGrade = grades_.GetJobGrade(sJob, k + 2);
                int sub = GetBaseToRank(sGrade, sLvl);
                sub = subJobZone ? GetSubJobStats(sGrade, sLvl, (ushort)sub) : sub / 2;
                m.Stats[k] = (ushort)(GetBaseToRank(familyRanks[k], mLvl) + GetBaseToRank(grades_.GetJobGrade(mJob, k + 2), mLvl) + sub);
            }

            int cappedLvl = Math.Min(mLvl, (byte)99);
            for (int i = SkillDivineMagic; i <= SkillBlueMagic; i++)
            {
                ushort maxSkill = growth_.GetMaxSkill(i, mJob, cappedLvl);
                if (maxSkill != 0) m.WorkingSkills[i] = maxSkill;
                else
                {
                    ushort maxSubSkill = growth_.GetMaxSkill(i, sJob, cappedLvl);
                    if (maxSubSkill != 0) m.WorkingSkills[i] = maxSubSkill;
                }
            }
            for (int i = Skill.HandToHand; i <= Skill.Staff; i++)
            {
                ushort maxSkill = growth_.GetMaxSkillByRank(3, cappedLvl);
                if (maxSkill != 0) m.WorkingSkills[i] = maxSkill;
            }

            m.AddModifier(Mod.DEF, (short)GetBaseDefEva(m, m.DefRank));
            m.AddModifier(Mod.EVA, (short)GetBaseDefEva(m, JobSkillRankToBaseEvaRank(mJob, sJob)));
            m.AddModifier(Mod.ATT, (short)GetBaseSkill(m, m.AttRank));
            m.AddModifier(Mod.ACC, (short)GetBaseSkill(m, m.AccRank));
            m.AddModifier(Mod.RATT, (short)GetBaseSkill(m, m.AttRank));
            m.AddModifier(Mod.RACC, (short)GetBaseSkill(m, m.AccRank));
            if (m.GetMobMod(MobMod.CanParry) > 0) m.WorkingSkills[SkillParry] = GetBaseSkill(m, m.GetMobMod(MobMod.CanParry));
            if ((mJob == JobMnk || mJob == JobPup) && m.GetMobMod(MobMod.CannotGuard) == 0) m.WorkingSkills[SkillGuard] = GetBaseSkill(m, 3);
            m.AddModifier(Mod.MEVA, (short)growth_.GetMaxSkillByRank(7, cappedLvl));

            // add traits for sub and main
            AddTraits?.Invoke(m);

            // Max [HP/MP] Boost traits
            CharStats.UpdateHealth(m);
            if (recover)
            {
                m.Tp = 0;
                m.Hp = m.MaxHp;
                m.Mp = m.MaxMp;
            }

            SetupJob(m);
            m.SubDamage = m.IsDualWielding ? GetBaseWeaponDamage(m) : (ushort)0;
            SetupRoaming(m);
            if (m.Ecosystem == Zone.Ecosystem.Beastmen) m.DefaultMobMod(MobMod.GilBonus, 100);
            if (m.PMaster != null) SetupPetSkills(m);
            m.Behavior |= (ushort)m.GetMobMod(MobMod.Behavior);
            if ((m.Type & MobType.Battlefield) != 0)
            {
                // SetupBattlefieldMob, before the battlefield's own setup
                m.SetMobMod(MobMod.NoDespawn, 1);
                m.SetMobMod(MobMod.GilMax, -1);
                m.SetMobMod(MobMod.MugGil, -1);
                m.SetMobMod(MobMod.ExpBonus, -100);
                m.DespawnAtMs = 0;
                // outside a battlefield: do not roam around, and superlink with the rest of the area (bcnmIDs 864
                // desires of emptiness, 704 darkness named and 706 waking dreams do not)
                if (m.PBattlefield == null)
                {
                    m.SetMobMod(MobMod.RoamResetFacing, 1);
                    m.SetMobMod(MobMod.RoamDistance, 0);
                    m.MaxRoamDistance = 0.0f;
                    if (m.BcnmId != 864 && m.BcnmId != 704 && m.BcnmId != 706) m.SetMobMod(MobMod.Superlink, m.BattlefieldArea);
                }
            }
            if (m.IsNotorious) m.SetMobMod(MobMod.NoDespawn, 1);
            if (instanced)
            {
                // SetupDungeonInstanceMob
                m.SetMobMod(MobMod.GilMax, 0);
                m.SetMobMod(MobMod.MugGil, 0);
                m.SetMobMod(MobMod.NoDespawn, 1);
            }
            if ((m.Type & MobType.Event) != 0)
            {
                // SetupEventMob
                m.RoamFlags |= RoamFlag.Scripted;
                m.SetMobMod(MobMod.RoamResetFacing, 1);
                m.MaxRoamDistance = 0.5f;
                m.SetMobMod(MobMod.NoDespawn, 1);
            }
            // if (PMob->CanStealGil()) PMob->ResetGilPurse();
            GilPurse?.Invoke(m);
        }

        // SetupPetSkills: the same mob can spawn as different species, so an avatar's skill list goes by species
        private static void SetupPetSkills(MobEntity m)
        {
            short skillListId = m.Species switch
            {
                248 => 715, // ifrit
                255 => 716, // titan
                249 => 717, // levi
                247 => 718, // garuda
                253 => 719, // shiva
                252 => 720, // ramuh
                243 => 721, // carbuncle
                _ => 0,
            };
            if (skillListId != 0) m.SetMobMod(MobMod.SkillList, skillListId);
        }

        private static bool HasMp(byte job) =>
            job == JobPld || job == JobWhm || job == JobBlm || job == JobRdm || job == JobDrk || job == JobBlu || job == JobSch || job == JobSmn;

        // grade::GetMobHPScale: base HP, job scale and the level 30+ scale per HP grade
        private static readonly byte[,] MobHpScale =
        {
            { 0, 0, 0 }, { 36, 9, 1 }, { 33, 8, 1 }, { 32, 7, 1 }, { 29, 6, 0 }, { 27, 5, 0 }, { 24, 4, 0 }, { 22, 3, 0 },
        };

        public static uint CalculateBaseMobHP(byte lvl, byte baseHp, byte jobScale, byte scaleX)
        {
            if (lvl == 0) return 0;
            int l5 = Math.Min((int)lvl, 5), l30 = Math.Min((int)lvl, 30);
            int rankIncrement = l5 <= 2 ? 0 : l5 == 3 ? 3 : l5 == 4 ? 7 : 14;
            int hp = baseHp + (l5 - 1) * (jobScale + 5) + rankIncrement;
            if (lvl > 5) hp += (l30 - 5) * (2 * jobScale + l30 + 6) / 2;
            if (lvl > 30) hp += (lvl - 30) * (63 + scaleX) + (lvl - 31) * (jobScale + 6);
            return (uint)hp;
        }

        public static uint CalculateSubjobHP(byte lvl, byte jobScale, byte scaleX)
        {
            int l = lvl > 49 ? lvl : lvl > 39 ? lvl * 3 / 4 : lvl > 30 ? lvl / 2 : lvl > 24 ? lvl / 4 : 0;
            return SubjobHPAtLevel(l, jobScale, scaleX);
        }

        // SubjobHPAtLevel: the sub job's HP at a sub job level
        public static uint SubjobHPAtLevel(int l, byte jobScale, byte scaleX)
        {
            double hp = jobScale * Math.Max(l - 1, 0) + (0.5 + 0.5 * scaleX) * Math.Max(l - 10, 0) + Math.Max(l - 30, 0) +
                        Math.Max(l - 50, 0) + Math.Max(l - 70, 0);
            return (uint)Math.Ceiling(hp / 2.0);
        }

        // mobutils::MonipulatorBaseHP: before merits and the species' multiplier, the mob main job term and the sub job
        // term at the full main level (retail Spriggans weight the level 1 base two to one between the jobs)
        public static uint MonipulatorBaseHP(Grades grades, byte mjob, byte sjob, byte level)
        {
            byte mGrade = grades.GetJobGrade(mjob, 0), sGrade = grades.GetJobGrade(sjob, 0);
            byte baseHp = (byte)((2 * MobHpScale[mGrade, 0] + MobHpScale[sGrade, 0]) / 3);
            uint mainHp = CalculateBaseMobHP(level, baseHp, MobHpScale[mGrade, 1], MobHpScale[mGrade, 2]);
            return mainHp + SubjobHPAtLevel(level, MobHpScale[sGrade, 1], MobHpScale[sGrade, 2]);
        }

        // GetBaseWeaponDamage: the level, with the region's offsets left in the DamageOffset mob mods
        public static ushort GetBaseWeaponDamage(MobEntity m)
        {
            byte region = ZoneRegions.Of(m.Zone);
            int offset, rangedOffset;
            if (region <= 27) { offset = 2; rangedOffset = 4; }
            else if (region <= 32) { offset = 10; rangedOffset = 12; }
            else if (region <= 45) { offset = 11; rangedOffset = 13; }
            else { offset = 2; rangedOffset = 5; }
            if (m.Type != MobType.Notorious && (m.Zone == 100 || m.Zone == 101 || m.Zone == 106 || m.Zone == 107 || m.Zone == 115 || m.Zone == 116))
            {
                offset--;
                rangedOffset--;
            }
            m.SetMobMod(MobMod.DamageOffset, (short)Math.Max(offset, 0));
            m.SetMobMod(MobMod.RangedDamageOffset, (short)Math.Max(rangedOffset, 0));
            return m.MLvl;
        }

        // GetBaseSkill: the rank's skill cap, read off a job that has it (A+ great axe .. E throwing)
        public ushort GetBaseSkill(MobEntity m, int rank) => rank switch
        {
            1 => growth_.GetMaxSkill(Skill.GreatAxe, JobWar, m.MLvl),
            2 => growth_.GetMaxSkill(Skill.Staff, JobWar, m.MLvl),
            3 => growth_.GetMaxSkill(Skill.Evasion, JobWar, m.MLvl),
            4 => growth_.GetMaxSkill(Skill.Archery, JobWar, m.MLvl),
            5 => growth_.GetMaxSkill(Skill.Throwing, JobMnk, m.MLvl),
            _ => 0,
        };

        public static ushort GetBaseDefEva(MobEntity m, int rank)
        {
            int l = m.MLvl;
            bool high = l > 50;
            double v = rank switch
            {
                1 => high ? 153 + (l - 50) * 5.0 : 6 + (l - 1) * 3.0,
                2 => high ? 147 + (l - 50) * 4.9 : 5 + (l - 1) * 2.9,
                3 => high ? 142 + (l - 50) * 4.8 : 5 + (l - 1) * 2.8,
                4 => high ? 136 + (l - 50) * 4.7 : 4 + (l - 1) * 2.7,
                5 => high ? 126 + (l - 50) * 4.5 : 4 + (l - 1) * 2.5,
                _ => 0,
            };
            return (ushort)Math.Floor(v);
        }

        // JobSkillRankToBaseEvaRank: the better evasion rank of the two jobs, as a def / eva rank
        public byte JobSkillRankToBaseEvaRank(byte mJob, byte sJob)
        {
            byte main = growth_.GetSkillRank(Skill.Evasion, mJob);
            byte sub = sJob == 0 ? main : growth_.GetSkillRank(Skill.Evasion, sJob);
            return Math.Min(main, sub) switch
            {
                1 or 2 => 1,
                3 or 4 or 5 => 2,
                6 or 7 or 8 => 3,
                9 => 4,
                10 => 5,
                _ => 3,
            };
        }

        public static ushort GetBaseToRank(int rank, int lvl) => rank switch
        {
            1 => (ushort)(5 + (lvl - 1) * 50 / 100),
            2 => (ushort)(4 + (lvl - 1) * 45 / 100),
            3 => (ushort)(4 + (lvl - 1) * 40 / 100),
            4 => (ushort)(3 + (lvl - 1) * 35 / 100),
            5 => (ushort)(3 + (lvl - 1) * 30 / 100),
            6 => (ushort)(2 + (lvl - 1) * 25 / 100),
            7 => (ushort)(2 + (lvl - 1) * 20 / 100),
            _ => 0,
        };

        // CheckSubJobZone: the zones where a mob's sub job stats follow GetSubJobStats instead of halving
        public static bool CheckSubJobZone(ushort z) =>
            (z >= 100 && z <= 128) || (z >= 139 && z <= 154) || (z >= 157 && z <= 163) || (z >= 165 && z <= 170) ||
            (z >= 172 && z <= 174) || (z >= 176 && z <= 181) || z == 184 || (z >= 190 && z <= 198) || (z >= 200 && z <= 209) ||
            (z >= 211 && z <= 213) || z == 220 || z == 221 || z == 227 || z == 228;

        public static ushort GetSubJobStats(int rank, int level, ushort stat)
        {
            float s = stat;
            float v;
            switch (rank)
            {
                case 1:
                    if (level <= 30) v = MathF.Max(MathF.Floor(s / (4.0f - 0.225f * (level - 30))), 2.0f);
                    else if (level <= 40) v = MathF.Floor(s / (3.25f - 0.073f * (level - 30)));
                    else if (level <= 46) v = MathF.Floor(s / (2.55f - 0.001f * (level - 41)));
                    else v = MathF.Floor(s / (2.7f - 0.001f * (level - 45)));
                    break;
                case 2:
                    if (level <= 30) v = MathF.Max(MathF.Floor(s / (3.1f - 0.075f * (level - 32))), 2.0f);
                    else if (level <= 40) v = MathF.Floor(s / (3.1f - 0.075f * (level - 32)));
                    else if (level <= 45) v = MathF.Floor(s / (2.5f - 0.025f * (level - 40)));
                    else v = MathF.Floor(s / (2.35f - 0.04f * (level - 44)));
                    break;
                case 3:
                    if (level <= 30) v = MathF.Max(MathF.Floor(s / (4.5f - 0.15f * (level - 26))), 2.0f);
                    else if (level <= 40) v = MathF.Floor(s / (3.28f - 0.001f * (level - 30)));
                    else if (level <= 45) v = MathF.Floor(s / (2.6f - 0.025f * (level - 40)));
                    else v = MathF.Floor(s / (2.1f - 0.2f * (level - 49)));
                    break;
                case 4:
                    if (level <= 30) v = MathF.Max(MathF.Floor(s / (5.0f - 0.05f * (level - 21))), 1.0f);
                    else if (level <= 40) v = MathF.Floor(s / (3.2f - 0.001f * (level - 29)));
                    else if (level <= 45) v = MathF.Floor(s / (3.5f - 0.08f * (level - 32)));
                    else v = MathF.Floor(s / (3.25f - 0.045f * (level - 32)));
                    break;
                case 5:
                    if (level <= 30) v = MathF.Max(MathF.Floor(s / (3.8f - 0.1f * (level - 32))), 1.0f);
                    else if (level <= 40) v = MathF.Floor(s / (3.8f - 0.15f * (level - 32)));
                    else if (level <= 45) v = MathF.Floor(s / (2.7f - 0.075f * (level - 40)));
                    else v = MathF.Floor(s / (2.7f - 0.05f * (level - 45)));
                    break;
                case 6:
                    if (level <= 30) v = MathF.Max(MathF.Floor(s / (4.0f - 0.15f * (level - 35))), 1.0f);
                    else if (level <= 40) v = MathF.Floor(s / (4.0f - 0.15f * (level - 30)));
                    else if (level <= 46) v = MathF.Floor(s / (3.0f - 0.1125f * (level - 40)));
                    else v = MathF.Floor(s / (3.0f - 0.07f * (level - 40)));
                    break;
                case 7:
                    if (level <= 30) v = MathF.Max(MathF.Floor(s / (4.0f - 0.15f * (level - 35))), 1.0f);
                    else if (level <= 40) v = MathF.Floor(s / (4.0f - 0.2f * (level - 31)));
                    else if (level <= 46) v = MathF.Floor(s / (2.5f - 0.09f * (level - 40)));
                    else v = stat / 2;
                    break;
                default:
                    v = s / 2.0f;
                    break;
            }
            return (ushort)v;
        }

        // SetupJob: the caster job's spell timers, then the main job's special skill
        public void SetupJob(MobEntity m)
        {
            byte mJob = m.MJob;
            byte job = grades_.GetJobGrade(mJob, 1) > 0 || mJob == JobNin || mJob == JobBrd ? mJob : m.SJob;
            switch (job)
            {
                case JobBlm:
                    m.DefaultMobMod(MobMod.MagicCool, 35);
                    m.DefaultMobMod(MobMod.GaChance, 40);
                    m.DefaultMobMod(MobMod.BuffChance, 15);
                    m.DefaultMobMod(MobMod.SevereSpellChance, 20);
                    break;
                case JobPld: case JobDrk: case JobWhm: case JobBlu: case JobSch: case JobGeo: case JobRun:
                    m.DefaultMobMod(MobMod.MagicCool, 35);
                    break;
                case JobBrd:
                    m.DefaultMobMod(MobMod.MagicCool, 35);
                    m.DefaultMobMod(MobMod.GaChance, 25);
                    m.DefaultMobMod(MobMod.BuffChance, 60);
                    break;
                case JobRdm:
                    m.DefaultMobMod(MobMod.MagicCool, 35);
                    m.DefaultMobMod(MobMod.GaChance, 15);
                    m.DefaultMobMod(MobMod.BuffChance, 40);
                    break;
                case JobSmn:
                    m.DefaultMobMod(MobMod.MagicCool, 70);
                    m.DefaultMobMod(MobMod.BuffChance, 100);
                    break;
                case JobNin:
                    m.DefaultMobMod(MobMod.SpecialCool, 9);
                    m.DefaultMobMod(MobMod.MagicCool, 35);
                    m.DefaultMobMod(MobMod.BuffChance, 20);
                    break;
            }

            switch (mJob)
            {
                case JobThf:
                    if (m.Ecosystem == Zone.Ecosystem.Beastmen) m.DefaultMobMod(MobMod.GilBonus, 150);
                    break;
                case JobRng:
                    if (m.Family == 57)
                    {
                        m.DefaultMobMod(MobMod.SpecialSkill, 658);
                        m.DefaultMobMod(MobMod.SpecialCool, 14);
                    }
                    else if (m.Family == 72)
                    {
                        m.DefaultMobMod(MobMod.SpecialSkill, 1747);
                        m.DefaultMobMod(MobMod.StandbackCool, 0);
                        m.DefaultMobMod(MobMod.SpecialCool, 14);
                        m.DefaultMobMod(MobMod.HpStandback, 70);
                        break;
                    }
                    else if (m.Family == 131) m.DefaultMobMod(MobMod.SpecialSkill, 1388);
                    else if (m.Family == 67) m.DefaultMobMod(MobMod.SpecialSkill, 1123);
                    else if (m.Family == 88) m.DefaultMobMod(MobMod.SpecialSkill, 1152);
                    else if (m.Family == 172) SetupRangedAttack(m);
                    else m.DefaultMobMod(MobMod.SpecialSkill, 272);
                    m.DefaultMobMod(MobMod.StandbackCool, 6);
                    m.DefaultMobMod(MobMod.SpecialCool, 12);
                    m.DefaultMobMod(MobMod.HpStandback, 70);
                    break;
                case JobNin:
                    if (m.Family == 131)
                    {
                        m.DefaultMobMod(MobMod.SpecialSkill, 1388);
                        m.DefaultMobMod(MobMod.SpecialCool, 12);
                    }
                    else if (m.Family == 67) m.DefaultMobMod(MobMod.SpecialSkill, 1123);
                    else if (m.Family == 88) m.DefaultMobMod(MobMod.SpecialSkill, 1152);
                    else if (m.Family == 172)
                    {
                        m.SetMobMod(MobMod.DualWield, 1);
                        SetupRangedAttack(m);
                    }
                    else if (m.Family != 119)
                    {
                        m.DefaultMobMod(MobMod.SpecialSkill, 272);
                        m.DefaultMobMod(MobMod.SpecialCool, 12);
                    }
                    m.DefaultMobMod(MobMod.HpStandback, 70);
                    break;
                case JobBst:
                    m.DefaultMobMod(MobMod.SpecialCool, 70);
                    m.DefaultMobMod(MobMod.SpecialSkill, 1017);
                    break;
                case JobPup:
                    m.DefaultMobMod(MobMod.SpecialSkill, 1901);
                    m.DefaultMobMod(MobMod.SpecialCool, 720);
                    break;
                case JobBlm:
                    m.DefaultMobMod(MobMod.StandbackCool, 12);
                    m.DefaultMobMod(MobMod.HpStandback, 70);
                    break;
            }
        }

        // SetupRangedAttack
        private static void SetupRangedAttack(MobEntity m)
        {
            m.DefaultMobMod(MobMod.SpecialSkill, 0); // Need to remove the base ranged attack
            m.DefaultMobMod(MobMod.RangedAttackRange, 14);
            m.RangedAttackEnabled = true;
            m.RangedBaseDelay = 300;
        }

        public static void SetupRoaming(MobEntity m)
        {
            m.DefaultMobMod(MobMod.RoamDistance, 6);
            m.DefaultMobMod(MobMod.RoamTurns, 1);
            m.DefaultMobMod(MobMod.RoamCool, 20);
            m.DefaultMobMod(MobMod.RoamRate, 15);
            if ((m.RoamFlags & RoamFlag.Ambush) != 0)
            {
                m.Hidden = true;
                m.MaxRoamDistance = 2.0f;
                m.SetMobMod(MobMod.RoamDistance, 5);
                m.SetMobMod(MobMod.RoamTurns, 1);
            }
            if ((m.RoamFlags & RoamFlag.Scripted) != 0) m.SetMobMod(MobMod.RoamResetFacing, 1);
        }

        // CMobEntity::Spawn: a level in the spawn's range, fresh stats, and a new point in one of its regions. Not
        // ported: the enmity list, the stolen-item reset and the posse (Assist). onSpawn is luautils::OnMobSpawn,
        // before the idle despawn timer; the patrol or first roam that ends CMobEntity::Spawn is
        // ZoneHost.MobRoamOnSpawn, run after this.
        public void Spawn(MobEntity m, Random rng, bool instanced, long nowMs, Action<MobEntity> onSpawn = null)
        {
            m.Status = m.Allegiance == 0 ? NpcEntity.StatusUpdate : NpcEntity.StatusNormal;
            m.UpdateMask |= EntityUpdatePacket.UpdateHp;
            m.Animation = 0;
            m.HideName(false);
            m.OwnerId = 0;
            m.AnimationSub = (byte)m.GetMobMod(MobMod.SpawnAnimationsub);
            byte level = (byte)rng.Next(m.MinLevel, m.MaxLevel + 1);
            // the traits are recalculated in CalculateMobStats (the restored modifiers drop theirs)
            m.TraitList.Clear();
            m.MLvl = level == 0 ? (byte)1 : level;
            m.SLvl = m.MLvl;
            CalculateMobStats(m, true, instanced);
            // mobutils: the SpellList mob-mod override and GetAvailableSpells run after the stats (CMobEntity::Reset)
            ResolveSpells?.Invoke(m);
            if (m.Regions.Count > 0)
            {
                m.RoamRegion = m.Regions[rng.Next(m.Regions.Count)];
                // none walkable keeps the last spawn point (the C++ warns)
                if (m.RoamRegion.RandomPoint(rng, NavMeshOf?.Invoke(m.Zone)) is { } point) (m.SpawnX, m.SpawnY, m.SpawnZ) = point;
            }
            m.X = m.SpawnX;
            m.Y = m.SpawnY;
            m.Z = m.SpawnZ;
            m.Rotation = m.SpawnRotation;
            if ((m.RoamFlags & RoamFlag.Stealth) != 0)
            {
                m.HideName(true);
                m.Untargetable = true;
            }
            m.DespawnAtMs = 0;
            onSpawn?.Invoke(m);
            if (m.GetMobMod(MobMod.IdleDespawn) > 0) m.DespawnAtMs = nowMs + m.GetMobMod(MobMod.IdleDespawn) * 1000L;
        }
    }
}
