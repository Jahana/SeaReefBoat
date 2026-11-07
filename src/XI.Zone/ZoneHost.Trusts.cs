using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // Trusts: trustutils (LoadTrustList / BuildTrustData from spell_list, mob_pools at spell id + 5000 and the pool's
    // resistances; LoadTrust and LoadTrustStatsAndSkills; SpawnTrust, which makes a party formed by trusts for a solo
    // master), CZoneEntities' trust list (InsertTRUST under a dynamic targid, SpawnTRUSTs, trustTick, the char's
    // zone-out), CTrustEntity (Spawn, Die, PostTick's updates and GROUP_ATTR to the party, FadeOut, OnDespawn),
    // CCharEntity::RemoveTrust / ClearTrusts (on death, a zone line, a party joined, left or disbanded, a charm),
    // the release from the trust's menu (0x01A) and CTrustController: the roam (following the master or the trust
    // ahead in formation, a warp beyond 30 yalms, the rest that recovers 5% between fights; a charmed master sends them
    // away), the engage with the master (TrustEngageType), the combat tick (the master's target, the movement its
    // TrustDistance gives, the declump, the gambits of ZoneHost.Gambits.cs, CTrustController::Cast), the non-combat
    // follower and the attack state (hitting and hit as a pet). A trust's script is xi.actions.spells.trust.<name>.
    // Its TP skills and weapon skills are in ZoneHost.TrustSkills.cs.
    public sealed partial class ZoneHost
    {
        private const float TrustRoamDistance = 3.0f;   // CTrustController::RoamDistance
        private const float TrustSpawnDistance = 3.0f;  // CTrustController::SpawnDistance
        private const float TrustWarpDistance = 30.0f;  // CTrustController::WarpDistance
        private const float TrustCastingDistance = 15.0f; // CTrustController::CastingDistance
        // TRUST_MOVEMENT_TYPE (MobMod TrustDistance): stay put, follow the master in a fight, melee; else the distance
        private const int TrustMoveNoMove = -1, TrustMoveNonCombat = -2, TrustMoveMelee = 0;
        private const long TrustDespawnMs = 3000;       // CDespawnState's wait before the fade
        private const byte AnimDespawn = 2;             // xi::Animation::Despawn
        private const ushort MsgTrustPartyMessage = 711;
        private const ushort ModelIdCornelia = 3119;
        private static readonly long[] TrustHealTickDelaysMs = { 15000, 10000, 10000, 3000 }; // m_tickDelays

        // passiveTrustIDs: Sakura, Moogle, Star Sibyl, Kuyin Hathdenna, Brygid, Kupofried, Cornelia
        private static readonly HashSet<uint> PassiveTrustIds = new HashSet<uint> { 927, 931, 935, 950, 970, 978, 1002 };

        // TrustData: the pool's look, names, jobs, combat numbers, the species' ecosystem and the resistances. The
        // stat ranks ApplyStatRanks puts here never reach the trust (LoadTrust does not copy them, so a trust keeps
        // the mob defaults of 3), and neither do the speed defaults' 62 / 50 beyond the base and animation speed.
        private sealed class TrustInfo
        {
            public uint TrustId, Pool;
            public bool Passive;
            public string Name = "", PacketName = "";
            public ushort[] Look = new ushort[10];
            public byte Ecosystem, NamePrefix, ModelSize, MJob, SJob, CmbSkill;
            public float ModelHitboxSize;
            public ushort Species, CmbDmgMult, CmbDelay, MobSkillList, SpellList;
            public float HpScale = 1.0f, MpScale = 1.0f;
            public byte BaseSpeed = 62, AnimationSpeed = 50;
            public readonly List<(Mod Mod, short Value)> Resists = new List<(Mod, short)>();
        }

        private Dictionary<uint, TrustInfo> trustList_;
        private Dictionary<uint, List<(ushort ModId, short Value, bool IsMobMod)>> mobPoolMods_;

        // trustutils::LoadTrustList / BuildTrustData: every trust spell with its pool and the pool's resistances
        private Dictionary<uint, TrustInfo> TrustList()
        {
            if (trustList_ != null) return trustList_;
            trustList_ = new Dictionary<uint, TrustInfo>();
            PetList(); // the species by id
            var r = Exec("SELECT mob_pools.poolid, mob_pools.name, mob_pools.packet_name, mob_pools.modelid, mob_pools.speciesid, " +
                         "mob_pools.mJob, mob_pools.sJob, mob_pools.spellList, mob_pools.cmbSkill, mob_pools.cmbDelay, mob_pools.cmbDmgMult, " +
                         "mob_pools.name_prefix, mob_pools.skill_list_id, mob_pools.modelSize, mob_pools.modelHitboxSize, spell_list.spellid, " +
                         "slash_sdt, pierce_sdt, h2h_sdt, impact_sdt, magical_sdt, fire_sdt, ice_sdt, wind_sdt, earth_sdt, lightning_sdt, " +
                         "water_sdt, light_sdt, dark_sdt, fire_res_rank, ice_res_rank, wind_res_rank, earth_res_rank, lightning_res_rank, " +
                         "water_res_rank, light_res_rank, dark_res_rank, paralyze_res_rank, bind_res_rank, silence_res_rank, slow_res_rank, " +
                         "poison_res_rank, light_sleep_res_rank, dark_sleep_res_rank, blind_res_rank, stun_res_rank, gravity_res_rank " +
                         "FROM spell_list, mob_pools, mob_resistances WHERE spell_list.spellid >= 896 " +
                         "AND (spell_list.spellid + 5000) = mob_pools.poolid AND mob_pools.resist_id = mob_resistances.resist_id " +
                         "ORDER BY spell_list.spellid");
            while (r != null && r.Next())
            {
                uint trustId = r.Get<uint>("spellid");
                var t = new TrustInfo
                {
                    TrustId = trustId,
                    Passive = PassiveTrustIds.Contains(trustId),
                    Pool = r.Get<uint>("poolid"),
                    Name = r.Get<string>("name"),
                    PacketName = r.Get<string>("packet_name"),
                    Species = r.Get<ushort>("speciesid"),
                    MJob = r.Get<byte>("mJob"),
                    SJob = r.Get<byte>("sJob"),
                    SpellList = (ushort)r.Get<short>("spellList"),
                    CmbSkill = r.Get<byte>("cmbSkill"),
                    CmbDelay = r.Get<ushort>("cmbDelay"),
                    CmbDmgMult = r.Get<ushort>("cmbDmgMult"),
                    NamePrefix = r.Get<byte>("name_prefix"),
                    MobSkillList = r.Get<ushort>("skill_list_id"),
                    ModelSize = r.IsNull("modelSize") ? (byte)0 : r.Get<byte>("modelSize"),
                    ModelHitboxSize = Math.Max(0.0f, (r.IsNull("modelHitboxSize") ? 0 : r.Get<byte>("modelHitboxSize")) / 10.0f),
                };
                if (!r.IsNull("modelid"))
                {
                    var look = r.GetBlobBytes("modelid");
                    for (int i = 0; i < 10 && 2 * i + 1 < look.Length; i++) t.Look[i] = (ushort)(look[2 * i] | look[2 * i + 1] << 8);
                }
                SpeciesInfo species = null;
                speciesById_?.TryGetValue(t.Species, out species);
                if (species != null) t.Ecosystem = species.Ecosystem;
                foreach (var (column, mod) in PetResistColumns) t.Resists.Add((mod, r.Get<short>(column)));
                trustList_[trustId] = t;
            }
            return trustList_;
        }

        // mobutils::LoadSqlModifiers' pool half: mob_pool_mods by pool (mods and mob mods)
        private List<(ushort ModId, short Value, bool IsMobMod)> MobPoolMods(uint pool)
        {
            if (mobPoolMods_ == null)
            {
                mobPoolMods_ = new Dictionary<uint, List<(ushort, short, bool)>>();
                var r = Exec("SELECT poolid, modid, value, is_mob_mod FROM mob_pool_mods");
                while (r != null && r.Next())
                {
                    uint id = r.Get<uint>("poolid");
                    if (!mobPoolMods_.TryGetValue(id, out var list)) mobPoolMods_[id] = list = new List<(ushort, short, bool)>();
                    list.Add((r.Get<ushort>("modid"), r.Get<short>("value"), r.Get<byte>("is_mob_mod") != 0));
                }
            }
            return mobPoolMods_.TryGetValue(pool, out var mods) ? mods : null;
        }

        private static string TrustScriptFile(TrustEntity t) => $"./scripts/actions/spells/trust/{t.Name}.lua";

        // ---- LoadTrust ---------------------------------------------------------------------------------------------

        // trustutils::LoadTrust: a new trust behind its master (further back for each one already out), its stats
        // from the pool and the master's level, a mob's "weapon" damage halved and scaled by cmbDmgMult
        private TrustEntity LoadTrust(CharData master, uint trustId)
        {
            if (!TrustList().TryGetValue(trustId, out var data))
            {
                log_($"Could not look up trust data for id: {trustId}");
                return null;
            }
            var trust = new TrustEntity(master, data.TrustId, data.Passive);
            AttachEffects(trust);
            trust.SaveModifiers();
            trust.Zone = master.Zone;
            trust.OwnerId = master.Id;
            // spawn me randomly around master
            (trust.X, trust.Y, trust.Z) = NearPosition(master, TrustSpawnDistance + master.PTrusts.Count * TrustSpawnDistance, MathF.PI);
            trust.Rotation = master.Rotation;
            trust.Look = (ushort[])data.Look.Clone();
            trust.Name = data.Name;
            trust.Pool = data.Pool;
            trust.PacketName = data.PacketName;
            trust.NamePrefix = data.NamePrefix;
            trust.Species = data.Species;
            trust.MobSkillList = data.MobSkillList;
            trust.MpScale = data.MpScale;
            trust.BaseSpeed = data.BaseSpeed;
            trust.AnimationSpeed = data.AnimationSpeed;
            MobStats.UpdateSpeed(trust);
            trust.Status = NpcEntity.StatusNormal;
            trust.ModelSize = data.ModelSize;
            trust.ModelHitboxSize = data.ModelHitboxSize;
            trust.Ecosystem = data.Ecosystem;
            trust.MJob = data.MJob;
            trust.SJob = data.SJob;
            // assume level matches master
            trust.MLvl = master.MLvl;
            trust.SLvl = (byte)(master.MLvl / 2);
            foreach (var (mod, value) in data.Resists) trust.SetModifier(mod, value);

            LoadTrustStatsAndSkills(trust, data);

            // Use Mob formulas to work out base "weapon" damage, but scale down to reasonable values.
            float mobStyleDamage = MobStats.GetBaseWeaponDamage(trust);
            float adjustedDamage = mobStyleDamage * 0.5f * (data.CmbDmgMult / 100.0f);
            ushort finalDamage = (ushort)Math.Max(adjustedDamage, 1.0f);
            trust.WeaponSkill = data.CmbSkill;
            trust.MainDamage = trust.SubDamage = trust.RangedDamage = finalDamage;
            trust.MainDelay = trust.BaseDelay = data.CmbDelay;
            trust.RangedBaseDelay = data.CmbDelay;
            // To give any Trust multi-hit, you need to give them cmbSkill == HandToHand (1).
            if (data.CmbSkill == Skill.HandToHand) trust.SetMobMod(MobMod.DualWield, 1);
            if (GetMobSpellList(data.SpellList) != null) SetMobSpellList(trust, data.SpellList);
            return trust;
        }

        // trustutils::LoadTrustStatsAndSkills: the pool's mods, a player-style HP and MP from the HP / MP scale's
        // grade and the jobs, the stats from the (default) ranks and the jobs, the skills at their caps, the base
        // DEF / EVA / ATT / ACC, a player's (G rank) magic evasion, the traits and the job setup
        private void LoadTrustStatsAndSkills(TrustEntity trust, TrustInfo data)
        {
            var settings = loader_.Stats.Settings;
            // Alter Ego Expo HPP/MPP +50%, All Status Resistance +25%
            if (settings.TrustAlterEgoExpo > 0)
            {
                trust.AddModifier(Mod.HPP, 50);
                trust.AddModifier(Mod.MPP, 50);
                trust.AddModifier(Mod.STATUSRES, 25);
            }

            // mobutils::AddSqlModifiers: the species' mods, then the pool's
            SpeciesInfo species = null;
            speciesById_?.TryGetValue(trust.Species, out species);
            if (species?.Attributes is { } attributes)
            {
                foreach (var (mod, value) in attributes.Mods) trust.AddModifier(mod, value);
                foreach (var (mod, value) in attributes.MobMods) trust.SetMobMod(mod, value);
            }
            if (MobPoolMods(trust.Pool) is { } poolMods)
            {
                foreach (var (modId, value, isMobMod) in poolMods)
                {
                    if (isMobMod) trust.SetMobMod((MobMod)modId, value);
                    else trust.AddModifier((Mod)modId, value);
                }
            }

            var grades = loader_.Grades;
            byte mJob = trust.MJob, sJob = trust.SJob;
            int mLvl = trust.MLvl, sLvl = trust.SLvl;

            int mainLevelOver30 = Math.Clamp(mLvl - 30, 0, 30);
            int mainLevelUpTo60 = mLvl < 60 ? mLvl - 1 : 59;
            int mainLevelOver60To75 = Math.Clamp(mLvl - 60, 0, 15);
            int mainLevelOver75 = mLvl < 75 ? 0 : mLvl - 75;
            int mainLevelOver10 = mLvl < 10 ? 0 : mLvl - 10;
            int mainLevelOver50andUnder60 = Math.Clamp(mLvl - 50, 0, 10);
            int mainLevelOver60 = mLvl < 60 ? 0 : mLvl - 60;
            int subLevelOver10 = Math.Clamp(sLvl - 10, 0, 20);
            int subLevelOver30 = sLvl < 30 ? 0 : sLvl - 30;

            float Hp(int g) => PetHpScale[g, 0] + PetHpScale[g, 1] * mainLevelUpTo60 + PetHpScale[g, 2] * mainLevelOver30 +
                               PetHpScale[g, 3] * mainLevelOver60To75 + PetHpScale[g, 4] * mainLevelOver75;
            float Mp(int g) => PetMpScale[g, 0] + PetMpScale[g, 1] * mainLevelUpTo60 + PetMpScale[g, 2] * mainLevelOver60;

            // HP
            int grade = TrustScaleToGrade(data.HpScale);
            float raceStat = Hp(grade);
            float jobStat = Hp(grades.GetJobGrade(mJob, 0));
            int bonusStat = (mainLevelOver10 + mainLevelOver50andUnder60) * 2;
            float sJobStat = 0;
            if (sLvl > 0)
            {
                int g = grades.GetJobGrade(sJob, 0);
                sJobStat = PetHpScale[g, 0] + PetHpScale[g, 1] * (sLvl - 1) + PetHpScale[g, 2] * subLevelOver30 + subLevelOver30 + subLevelOver10;
            }
            float hpMultiplier = settings.AlterEgoHpMultiplier is >= 0.1f and <= 2.0f ? settings.AlterEgoHpMultiplier : 1.0f;
            trust.BaseMaxHp = (short)((raceStat + jobStat + bonusStat + sJobStat) * hpMultiplier);

            // MP
            raceStat = 0;
            jobStat = 0;
            sJobStat = 0;
            grade = TrustScaleToGrade(data.MpScale);
            if (grades.GetJobGrade(mJob, 1) == 0)
            {
                if (grades.GetJobGrade(sJob, 1) != 0 && sLvl > 0)
                    raceStat = (PetMpScale[grade, 0] + PetMpScale[grade, 1] * (sLvl - 1)) / grades.SjMpDivisor;
            }
            else raceStat = Mp(grade);
            int mpGrade = grades.GetJobGrade(mJob, 1);
            if (mpGrade > 0) jobStat = Mp(mpGrade);
            if (sLvl > 0)
            {
                int g = grades.GetJobGrade(sJob, 1);
                sJobStat = PetMpScale[g, 0] + PetMpScale[g, 1];
            }
            float mpMultiplier = settings.AlterEgoMpMultiplier is >= 0.1f and <= 2.0f ? settings.AlterEgoMpMultiplier : 1.0f;
            trust.BaseMaxMp = (short)((raceStat + jobStat + sJobStat) * mpMultiplier);

            trust.Tp = 0;
            CharStats.UpdateHealth(trust);
            trust.Hp = trust.MaxHp;
            trust.Mp = trust.MaxMp;

            // Stats: the ranks (the mob defaults), the main job's, half the sub job's past sub level 15
            float statMultiplier = settings.AlterEgoStatMultiplier is >= 0.1f and <= 2.0f ? settings.AlterEgoStatMultiplier : 1.0f;
            byte[] ranks = { trust.StrRank, trust.DexRank, trust.VitRank, trust.AgiRank, trust.IntRank, trust.MndRank, trust.ChrRank };
            for (int k = 0; k < 7; k++)
            {
                int f = MobStats.GetBaseToRank(ranks[k], mLvl);
                int m = MobStats.GetBaseToRank(grades.GetJobGrade(mJob, k + 2), mLvl);
                int s = MobStats.GetBaseToRank(grades.GetJobGrade(sJob, k + 2), sLvl);
                s = sLvl > 15 ? (ushort)s / 2 : 0;
                trust.Stats[k] = (ushort)((f + m + s) * statMultiplier);
            }

            // Skills
            var growth = loader_.Stats.Growth;
            float skillMultiplier = settings.AlterEgoSkillMultiplier;
            int cappedLvl = Math.Min(mLvl, 99);
            for (int i = PetSkillDivineMagic; i <= PetSkillBlueMagic; i++)
            {
                ushort maxSkill = growth.GetMaxSkill(i, mJob, cappedLvl);
                if (maxSkill != 0) trust.WorkingSkills[i] = (ushort)(maxSkill * skillMultiplier);
                else
                {
                    // set skill as high as main level, so their spells won't get resisted
                    ushort maxSubSkill = growth.GetMaxSkill(i, sJob, cappedLvl);
                    if (maxSubSkill != 0) trust.WorkingSkills[i] = (ushort)(maxSubSkill * skillMultiplier);
                }
            }
            // battleutils::GetMaxSkill(rank, level) with the skill id as the rank, as the C++ calls it
            for (int i = Skill.HandToHand; i <= Skill.Staff; i++)
            {
                ushort maxSkill = growth.GetMaxSkillByRank(i, cappedLvl);
                if (maxSkill != 0) trust.WorkingSkills[i] = (ushort)(maxSkill * skillMultiplier);
            }

            var stats = MobStatsFor();
            trust.AddModifier(Mod.DEF, (short)stats.GetBaseSkill(trust, trust.DefRank));
            trust.AddModifier(Mod.EVA, (short)stats.GetBaseSkill(trust, trust.EvaRank));
            trust.AddModifier(Mod.ATT, (short)stats.GetBaseSkill(trust, trust.AttRank));
            trust.AddModifier(Mod.ACC, (short)stats.GetBaseSkill(trust, trust.AccRank));
            trust.AddModifier(Mod.RATT, (short)stats.GetBaseSkill(trust, trust.AttRank));
            trust.AddModifier(Mod.RACC, (short)stats.GetBaseSkill(trust, trust.AccRank));
            // mobutils::GetMagicEvasion: trusts have G rank meva like players
            trust.AddModifier(Mod.MEVA, (short)growth.GetMaxSkillByRank(12, cappedLvl));

            TraitAddMobTraits(trust);
            stats.SetupJob(trust);
            LoadTrustTpSkills(trust);
        }

        // LoadTrustStatsAndSkills' scaleToGrade: an HP / MP scale around 1.0 mapped onto the grades 1-7 (unsigned, as
        // in the C++, so a scale under 0.7 wraps to grade 1)
        private static int TrustScaleToGrade(float input)
        {
            unchecked
            {
                uint multipliedInput = (uint)(input * 100U);
                uint output = (multipliedInput - 70U) * (7U - 1U) / (140U - 70U) + 1U;
                uint reverseMappedGrade = Math.Clamp(output, 1U, 7U);
                return (int)Math.Clamp(7U - reverseMappedGrade, 1U, 7U);
            }
        }

        // ---- spawning ----------------------------------------------------------------------------------------------

        // trustutils::SpawnTrust: the trust joins the master's party (a new one, formed by trusts, for a solo master),
        // takes its confrontation effects, goes into the zone and spawns; the party list is sent again
        private TrustEntity SpawnTrust(CharData master, uint trustId)
        {
            var trust = LoadTrust(master, trustId);
            if (trust == null) return null;
            if (master.Party == null)
            {
                var party = new Party(this, master);
                party.FormedByTrusts = true;
            }
            master.PTrusts.Add(trust);
            foreach (var e in master.StatusEffects.All)
                if (!e.Deleted && e.HasFlag(EffectFlag.Confrontation)) trust.StatusEffects.AddStatusEffect(e.Copy());
            if (master.PBattlefield != null) trust.PBattlefield = master.PBattlefield;
            InsertTrust(trust);
            TrustSpawn(trust);
            master.Party?.ReloadParty();
            return trust;
        }

        // CZoneEntities::InsertTRUST: a dynamic targid and id, the zone's trust list, the chars nearby, then no more
        // special spawn animation
        private void InsertTrust(TrustEntity trust)
        {
            var info = AreaOf(trust);
            info.AssignDynamicTargId(trust, NowMs());
            info.Trusts.Add(trust);
            foreach (var (c, s) in charSessions_)
            {
                if (!SameArea(c, trust) || !s.InZone || !TrustVisible(c, trust)) continue;
                if (c.SpawnTrustList.Add(trust.Id)) UpdateEntityPacket(s, trust, EntityUpdate.Spawn, EntityUpdatePacket.UpdateAllMob);
            }
            trust.SpawnAnimation = 0;
        }

        // CTrustEntity::Spawn: CBattleEntity::Spawn (no stat calculation, LoadTrust did it), OnMobSpawn, the health
        // again with what the script added and full; the master learns it is its trust (CEntitySetNamePacket)
        private void TrustSpawn(TrustEntity trust)
        {
            long now = NowMs();
            trust.Animation = Anim.None;
            trust.HideName(false);
            trust.Status = NpcEntity.StatusNormal;
            trust.UpdateMask |= EntityUpdatePacket.UpdateHp;
            trust.LocalVars.Clear();
            ResetEntityData(trust);
            ClearActionQueues(trust);
            trust.OwnerId = 0;
            MobControllerReset(trust, now);
            if (scripts_ != null && lua_ != null)
            {
                lua_.GetPath($"xi.actions.spells.trust.{trust.Name}");
                bool loaded = lua_.IsTable(-1);
                lua_.Pop(1);
                if (!loaded) scripts_.LoadLuaObjectFromFile(TrustScriptFile(trust));
            }
            OnMobSpawn(trust);
            // Recompute derived HP/MP after spawn-time modifiers (e.g. HPP/MPP)
            CharStats.UpdateHealth(trust);
            trust.Hp = trust.MaxHp;
            trust.Mp = trust.MaxMp;
            trust.UpdateMask |= EntityUpdatePacket.UpdateHp;
            if (trust.PMaster is CharData master) PushTo(master, EntitySetName(trust));
        }

        // CEntitySetNamePacket (0x067): the trust's targid and id, its master's targid and its packet name, so the
        // client shows the trust's menu (Release)
        private static BasicPacket EntitySetName(TrustEntity trust)
        {
            var w = new PacketWriter(0x067, 0x2C);
            var b = w.Packet.Buffer;
            b[0x04] = 0x03;
            b[0x05] = 0x05;
            w.U16(0x06, trust.TargId);
            w.U32(0x08, trust.Id);
            if (trust.PMaster is CharData master) w.U16(0x0C, master.TargId);
            // packBitsBE(buffer + 0x04, 0x18 + size, 0, 6, 10): the message size in the 10 bits from bit 6 of 0x04
            int size = 0x18 + trust.PacketName.Length;
            int v = b[0x04] | b[0x05] << 8;
            v = v & ~(0x3FF << 6) | (size & 0x3FF) << 6;
            b[0x04] = (byte)v;
            b[0x05] = (byte)(v >> 8);
            for (int i = 0; i < trust.PacketName.Length && 0x18 + i < b.Length; i++) b[0x18 + i] = (byte)trust.PacketName[i];
            b[0x10] = 0x04;
            return w.Packet;
        }

        // SpawnTRUSTs' visible: up (normal or update), at eye level and within 50 yalms
        private static bool TrustVisible(CharData c, TrustEntity trust)
        {
            if (trust.Status != NpcEntity.StatusNormal && trust.Status != NpcEntity.StatusUpdate) return false;
            if (Math.Abs(trust.Y - c.Y - 0.5f) > 20.0f) return false;
            float dx = c.X - trust.X, dy = c.Y - trust.Y, dz = c.Z - trust.Z;
            return dx * dx + dy * dy + dz * dz <= EntityRenderDistance * EntityRenderDistance;
        }

        // CZoneEntities::SpawnTRUSTs
        private void SpawnTrusts(MapSession s, CharData c)
        {
            var zone = AreaOf(c);
            var byId = new Dictionary<uint, TrustEntity>();
            foreach (var trust in zone.Trusts) byId[trust.Id] = trust;
            foreach (var id in new List<uint>(c.SpawnTrustList))
            {
                if (byId.TryGetValue(id, out var trust) && TrustVisible(c, trust)) continue;
                c.SpawnTrustList.Remove(id);
                if (trust != null) UpdateEntityPacket(s, trust, EntityUpdate.Despawn, 0);
            }
            foreach (var trust in zone.Trusts)
                if (TrustVisible(c, trust) && c.SpawnTrustList.Add(trust.Id)) UpdateEntityPacket(s, trust, EntityUpdate.Spawn, EntityUpdatePacket.UpdateAllMob);
        }

        // ---- leaving -----------------------------------------------------------------------------------------------

        // CTrustController::Despawn and CAIContainer::Despawn: no master, the despawn animation, and the despawn
        // state's 3 s before it fades out
        private void TrustDespawn(TrustEntity trust, long now)
        {
            if (trust.FadeAtMs != 0) return;
            trust.PMaster = null;
            trust.Animation = AnimDespawn;
            trust.UpdateMask |= EntityUpdatePacket.UpdateHp;
            AbortMagic(trust, now);
            AbortMobSkill(trust, now);
            if (trust.Engaged) MobDisengage(trust, now);
            trust.PathFind?.Clear();
            trust.FadeAtMs = now + TrustDespawnMs;
        }

        // CTrustEntity::OnDespawn: onMobDespawn unless it died, the fade out (the chars that had it spawned are told)
        // and the DESPAWN listeners
        private void TrustOnDespawn(TrustEntity trust)
        {
            if (trust.Hpp > 0) OnMobDespawn(trust);
            trust.FadeOut();
            trust.UpdateMask = 0;
            foreach (var (c, s) in charSessions_)
                if (c.SpawnTrustList.Remove(trust.Id)) UpdateEntityPacket(s, trust, EntityUpdate.Despawn, 0);
            TriggerListener(trust, "DESPAWN", trust);
        }

        // CCharEntity::RemoveTrust: a trust still up leaves the master's list and despawns; the party list follows
        private void RemoveTrust(CharData c, TrustEntity trust)
        {
            if (trust.Status == NpcEntity.StatusDisappear) return;
            if (c.PTrusts.Remove(trust)) TrustDespawn(trust, NowMs());
            c.ReloadParty = true;
        }

        // CCharEntity::ClearTrusts
        private void ClearTrusts(CharData c)
        {
            long now = NowMs();
            foreach (var trust in c.PTrusts) TrustDespawn(trust, now);
            c.PTrusts.Clear();
            c.ReloadParty = true;
        }

        // CZone::DecreaseZoneCounter's trusts: every char in the zone sees them go, and the char's spawn list empties
        private void RemoveCharTrusts(CharData c)
        {
            foreach (var trust in c.PTrusts)
                foreach (var (o, os) in charSessions_)
                    if (SameArea(o, c)) UpdateEntityPacket(os, trust, EntityUpdate.Despawn, 0);
            ClearTrusts(c);
            c.SpawnTrustList.Clear();
        }

        // CTrustEntity::Die: onMobDeath without a killer, its enmity gone, the death state (Internal_Die(0s):
        // CDeathState::init wears its death-flagged effects silently, the death animation, no path), out of its
        // master's list (and so despawning), then CBattleEntity::Die's DEATH listeners and no battle target
        private void TrustDie(TrustEntity trust, long now)
        {
            OnMobDeath(trust, null);
            trust.Enmity.Clear();
            AbortMagic(trust, now);
            AbortMobSkill(trust, now);
            trust.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Death, EffectNotice.Silent);
            trust.Animation = Anim.Death;
            trust.UpdateMask |= EntityUpdatePacket.UpdateHp;
            trust.PathFind?.Clear();
            trust.DeathAtMs = now;
            if (trust.PMaster is CharData master) RemoveTrust(master, trust);
            trust.OwnerId = 0;
            TriggerListener(trust, "DEATH", trust);
            trust.BattleTarget = null;
            trust.Engaged = false;
        }

        // the release from the trust's menu (GP_CLI_COMMAND_ACTION's trigger on a trust): its despawn message now, and
        // 2 s later (if it is still the char's) the special despawn and out of the party
        private bool ReleaseTrust(CharData c, ushort actIndex)
        {
            var trust = AreaOf(c).Trusts.Find(t => t.TargId == actIndex);
            if (trust == null) return false;
            if (trust.PMaster != c || trust.Released) return true;
            trust.Released = true;
            // note: this will get called a second time later, but PMaster is null by then, so the despawn message is
            // never emitted twice
            OnMobDespawn(trust);
            ushort targid = trust.TargId;
            QueueNative(c, 2000, false, entity =>
            {
                if (entity is not CharData owner) return;
                var delayed = AreaOf(owner).Trusts.Find(t => t.TargId == targid);
                if (delayed == null || delayed.PMaster != owner) return;
                // For some reason they use the death animation to play the special despawn.
                delayed.Animation = Anim.Death;
                delayed.UpdateMask |= EntityUpdatePacket.UpdateHp;
                RemoveTrust(owner, delayed);
            });
            return true;
        }

        // ---- the tick ----------------------------------------------------------------------------------------------

        // CZoneEntities::trustTick and CTrustEntity::PostTick: a gone trust leaves the mobs' enmity, the chars' spawn
        // lists and the zone (its targid freed 60 s later); the others run their controller every 400 ms and their
        // despawn timer, and send their updates at most every 250 ms (an HP change to the master's party as well)
        private void TickTrusts()
        {
            long now = NowMs();
            foreach (var (info, _, _) in AllAreas())
            {
                foreach (var trust in info.Trusts.ToArray())
                {
                    if (trust.Status == NpcEntity.StatusDisappear)
                    {
                        foreach (var m in info.Mobs) m.Enmity.Clear(trust.Id);
                        foreach (var (c, _) in charSessions_) c.SpawnTrustList.Remove(trust.Id);
                        info.Trusts.Remove(trust);
                        info.ReleaseDynamicTargId(trust.TargId, now);
                        continue;
                    }

                    if (trust.FadeAtMs != 0 && now >= trust.FadeAtMs)
                    {
                        trust.FadeAtMs = 0;
                        TrustOnDespawn(trust);
                        continue;
                    }

                    // CZoneEntities::trustTick: expired spell and ability recasts go (none ever ran out without this)
                    trust.Recasts?.Check();

                    if (trust.FadeAtMs == 0 && trust.DeathAtMs == 0 && now >= trust.NextAiTickMs)
                    {
                        trust.NextAiTickMs = now + MobAiTickMs;
                        TrustControllerTick(trust, now);
                        // the state on top: a skill, a spell, else the attack state (over once its target is down)
                        if (trust.SkillState != null) TickMobSkill(trust, now);
                        else if (trust.MagicState != null) TickMagic(trust, now);
                        else if (trust.RangedState != null) TickMobRanged(trust, now);
                        else if (trust.WsState is { } ws)
                        {
                            if (UpdateTrustWeaponSkill(trust, ws, now)) EndTrustWeaponSkill(trust, ws, now);
                        }
                        else if (TickMobAbility(trust, now)) { }
                        else if (trust.Engaged && trust.DeathAtMs == 0)
                        {
                            if (TrustTargetGone(trust)) TrustDisengage(trust, now);
                            else UpdatePetAttackState(trust, now);
                        }
                    }

                    // CBattleEntity::PostTick: out of HP and not yet dying
                    if (trust.Hp <= 0 && trust.DeathAtMs == 0 && trust.FadeAtMs == 0) TrustDie(trust, now);

                    if (trust.UpdateMask != 0 && now > trust.NextUpdateMs)
                    {
                        trust.NextUpdateMs = now + 250;
                        foreach (var (c, s) in charSessions_)
                            if (InArea(c, info) && c.SpawnTrustList.Contains(trust.Id)) UpdateEntityPacket(s, trust, EntityUpdate.Update, trust.UpdateMask);
                        if (trust.PMaster is CharData { Party: { } party } && (trust.UpdateMask & EntityUpdatePacket.UpdateHp) != 0)
                            foreach (var m in party.Members.ToArray()) PushTo(m, TrustGroupAttr(trust));
                        trust.UpdateMask = 0;
                    }
                }
            }
        }

        // CTrustController::Tick: nothing without a master, sent away when the master is charmed; a non-combat trust
        // keeps following while its master fights, the others fight (engaged) or roam
        private void TrustControllerTick(TrustEntity trust, long now)
        {
            if (trust.PMaster is not CharData master) return;
            if (master.IsCharmed)
            {
                TrustDespawn(trust, now);
                return;
            }
            bool nonCombatFollowTrust = trust.GetMobMod(MobMod.TrustDistance) == TrustMoveNonCombat;
            if (master.AttackTarget != null && nonCombatFollowTrust) TrustNonCombatTick(trust, master, now);
            else if (trust.Engaged) TrustCombatTick(trust, master, now);
            else if (trust.IsAlive && !trust.IsDead) TrustRoamTick(trust, master, now);
        }

        // CAttackState::Update's end: no target, or a dead one
        private static bool TrustTargetGone(TrustEntity trust) =>
            trust.BattleTarget is not MobEntity t || !t.IsAlive || t.IsDead || !t.IsSpawned || !SameArea(t, trust);

        // CAIContainer::Internal_Engage for a trust: CAttackState (the first swing after the engage delay) and
        // CMobEntity::OnEngage, without CMobController::Engage's extras
        private void TrustEngage(TrustEntity trust, BattleEntity target, long now)
        {
            if (target == null) return;
            trust.BattleTarget = target;
            if (trust.Engaged) return;
            trust.Engaged = true;
            trust.BattleStartMs = now;
            trust.SwingDelayMs = MeleeFor().WeaponDelay(trust, false);
            trust.SwingAtMs = now + MobEngageDelayMs;
            trust.Animation = Anim.Attack;
            trust.UpdateMask |= EntityUpdatePacket.UpdateHp;
            TriggerListener(trust, "ENGAGE", trust, target);
            OnMobEngage(trust, target);
        }

        // CAIContainer::Internal_Disengage for a trust: CMobEntity::OnDisengage (the path and enmity cleared, neutral,
        // no owner, CBattleEntity::OnDisengage's target and animation, onMobDisengage)
        private void TrustDisengage(TrustEntity trust, long now)
        {
            if (!trust.Engaged) return;
            trust.Engaged = false;
            trust.PathFind?.Clear();
            trust.Enmity.Clear();
            trust.Neutral = true;
            trust.OwnerId = 0;
            trust.BattleTarget = null;
            if (trust.Animation == Anim.Attack) trust.Animation = Anim.None;
            trust.UpdateMask |= EntityUpdatePacket.UpdateHp;
            TriggerListener(trust, "DISENGAGE", trust);
            OnMobDisengage(trust);
        }

        // CPathFind::WarpTo: next to the point, facing it
        private void TrustWarpTo(TrustEntity trust, BattleEntity to)
        {
            PathOf(trust).Clear();
            (trust.X, trust.Y, trust.Z) = NearPosition(to, 2.0f, MathF.PI);
            trust.Rotation = WorldAngle(trust.X, trust.Z, trust.Rotation, to.X, to.Z);
            trust.UpdateMask |= EntityUpdatePacket.UpdatePos;
        }

        // sidestepPosition: a quarter turn off the line to the reference point, by the offset, at its height
        private static (float X, float Y, float Z) SidestepPosition(BattleEntity from, BattleEntity reference, float offset)
        {
            byte perpendicularAngle = (byte)(WorldAngle(from.X, from.Z, from.Rotation, reference.X, reference.Z) + 64);
            float radians = ZoneLine.RotationToRadian(perpendicularAngle);
            return (from.X - MathF.Cos(radians) * offset, reference.Y, from.Z + MathF.Sin(radians) * offset);
        }

        // CTrustController::DoCombatTick: off when the master is not fighting; onto the master's target once the
        // master has hate on it; still while casting or shooting; else the warp, facing the target and the movement its
        // TrustDistance gives (staying in casting range, melee range or out at its distance), the declump, the path, then
        // the gambits and COMBAT_TICK
        private void TrustCombatTick(TrustEntity trust, CharData master, long now)
        {
            var masterMob = master.BattleTarget as MobEntity;
            var target = trust.BattleTarget;

            if (master.AttackTarget == null)
            {
                TrustDisengage(trust, now);
                trust.CombatEndMs = now;
            }

            if (masterMob != null && trust.BattleTarget != master.BattleTarget && masterMob.Enmity.Get(master.Id) is { Active: true } entry && entry.CE + entry.VE > 0)
            {
                // Internal_ChangeTarget
                trust.BattleTarget = masterMob;
            }

            // If busy, don't run around!
            if (trust.MagicState != null || trust.RangedState != null) return;

            if (target == null) return;
            var path = PathOf(trust);
            if (trust.InactiveState == null && trust.Speed > 0)
            {
                float currentDistanceToTarget = Distance(trust, target);
                float currentDistanceToMaster = Distance(trust, master);

                if (master.AttackTarget == null && currentDistanceToTarget > TrustWarpDistance) TrustWarpTo(trust, target);

                path.LookAt(target.X, target.Y, target.Z);

                int movementDistance = trust.GetMobMod(MobMod.TrustDistance);
                switch (movementDistance)
                {
                    case TrustMoveNoMove:
                        if (currentDistanceToMaster > TrustCastingDistance) TrustPathOutToDistance(trust, target, 9.0f, now);
                        else if (currentDistanceToTarget > TrustCastingDistance) TrustPathOutToDistance(trust, target, 9.0f, now);
                        break;
                    case TrustMoveNonCombat:
                        // Non-combat followers should not use target-distance positioning.
                        break;
                    case TrustMoveMelee:
                        // CBattleEntity::CanAttack: in melee range
                        if (currentDistanceToTarget > MeleeRange(trust, target) && currentDistanceToTarget > TrustRoamDistance)
                        {
                            if (currentDistanceToTarget < TrustRoamDistance * 3.0f &&
                                path.PathAround(target.X, target.Y, target.Z, TrustRoamDistance, PathFlag.Run))
                            {
                                path.FollowPath(now);
                            }
                            else
                            {
                                path.StepTo(target.X, target.Y, target.Z, true);
                            }
                        }
                        break;
                    default: // MID_RANGE, LONG_RANGE or the positive distance
                        TrustPathOutToDistance(trust, target, movementDistance, now);
                        break;
                }

                if (!path.IsFollowingPath) TrustDeclump(trust, master, target);
            }

            if (!trust.InTransit) path.FollowPath(now);

            GambitsTick(trust, master, now);
            TriggerListener(trust, "COMBAT_TICK", trust, master, target);
        }

        // CTrustController::DoNonCombatTick: following the master (the first trust, 3 yalms back) or the trust ahead,
        // sidestepping a trust it stands on, the warp; the gambits and COMBAT_TICK while the master fights and it stands
        private void TrustNonCombatTick(TrustEntity trust, CharData master, long now)
        {
            // Keep COMBAT_TICK target valid for listeners/gambits.
            trust.BattleTarget = master.BattleTarget;
            var target = trust.BattleTarget;

            int currentPartyPos = TrustPartyPosition(master, trust);
            BattleEntity followTarget = master;
            if (currentPartyPos > 0 && currentPartyPos - 1 < master.PTrusts.Count && master.PTrusts[currentPartyPos - 1] != trust)
                followTarget = master.PTrusts[currentPartyPos - 1];

            // First trust keeps a bit more space from master.
            const float firstTrustFollowDistance = 3.0f;
            float desiredFollowDistance = currentPartyPos == 0 ? firstTrustFollowDistance : TrustRoamDistance;
            float currentDistance = Distance(trust, followTarget);

            var path = PathOf(trust);
            // Simple declump so non-combat trusts don't stack on each other.
            foreach (var other in master.PTrusts)
            {
                if (other != trust && Distance(other, trust) < 1.0f && !path.IsFollowingPath)
                {
                    // Sidestep the trust we are stacked on, with odd/even party slots taking opposite sides.
                    float amount = currentPartyPos % 2 != 0 ? 1.0f : -1.0f;
                    var (x, y, z) = SidestepPosition(trust, other, amount);
                    if (path.ValidPosition(x, y, z, now) && path.PathTo(x, y, z, PathFlag.Run)) path.FollowPath(now);
                    break;
                }
            }

            if (currentDistance > TrustWarpDistance)
            {
                TrustWarpTo(trust, followTarget);
            }
            else if (currentDistance > desiredFollowDistance)
            {
                if (currentDistance < desiredFollowDistance * 3.0f &&
                    path.PathAround(followTarget.X, followTarget.Y, followTarget.Z, desiredFollowDistance, PathFlag.Run))
                {
                    path.FollowPath(now);
                }
                else if (trust.Speed > 0)
                {
                    path.StepTo(followTarget.X, followTarget.Y, followTarget.Z, true);
                }
            }

            if (path.IsFollowingPath) path.FollowPath(now);

            // Keep gambits active in combat, but only while stationary.
            if (master.AttackTarget != null && !path.IsFollowingPath)
            {
                GambitsTick(trust, master, now);
                TriggerListener(trust, "COMBAT_TICK", trust, master, target);
            }
        }

        // CTrustController::Declump: off a trust within 1.5 yalms (not itself moving), sidestepping around the target by
        // up to 1.5 yalms, odd and even party places to opposite sides
        private void TrustDeclump(TrustEntity trust, CharData master, BattleEntity target)
        {
            int currentPartyPos = TrustPartyPosition(master, trust);
            foreach (var other in master.PTrusts)
            {
                if (other == trust || (other.PathFind?.IsFollowingPath ?? false) || Distance(other, trust) >= 1.5f) continue;
                // Sidestep relative to the shared target so trusts spread around the enemy rather than away from each other.
                float moveAmount = (float)MobRng.NextDouble() * 1.5f * (currentPartyPos % 2 != 0 ? 1.0f : -1.0f);
                var (x, y, z) = SidestepPosition(trust, target, moveAmount);
                if (PathOf(trust).ValidPosition(x, y, z, NowMs())) PathOf(trust).PathTo(x, y, z, PathFlag.Run);
                break;
            }
        }

        // CTrustController::PathOutToDistance: a new spot at the distance around the target (at most every 3 s, when
        // out of the band of 2.5 yalms, not already on the way), then running there unless close or it keeps the hate
        // (3 tries), else facing the target. A trust off the navmesh repositions too; of 5 random spots the first on
        // the mesh and in sight is taken.
        private void TrustPathOutToDistance(TrustEntity trust, BattleEntity target, float amount, long now)
        {
            var path = PathOf(trust);
            float currentDistanceToTarget = Distance(trust, target);
            var targetPosition = (X: trust.X, Y: trust.Y, Z: trust.Z);

            if (TrustTopEnmity(trust) == trust) ++trust.FailedRepositionAttempts;
            else trust.FailedRepositionAttempts = 0;

            // Invalidate position and pick new one (limit: every 3s)
            if ((currentDistanceToTarget < amount - 2.5f || currentDistanceToTarget > amount + 2.5f || !path.ValidPosition(trust.X, trust.Y, trust.Z, now)) &&
                now - trust.LastRepositionMs > 3000 && !trust.InTransit)
            {
                var positions = new (float X, float Y, float Z)[5];
                for (int i = 0; i < positions.Length; i++)
                {
                    float radians = ZoneLine.RotationToRadian((byte)MobRng.Next(256));
                    positions[i] = (target.X - MathF.Cos(radians) * amount, target.Y, target.Z + MathF.Sin(radians) * amount);
                }
                foreach (var potential in positions)
                {
                    if (!path.ValidPosition(potential.X, potential.Y, potential.Z, now) || !CanSeeTarget(trust, potential.X, potential.Y, potential.Z)) continue;
                    targetPosition = potential;
                    trust.InTransit = true;
                    break;
                }
                trust.LastRepositionMs = now;
            }

            // Get somewhat close to the target destination
            float dx = trust.X - targetPosition.X, dy = trust.Y - targetPosition.Y, dz = trust.Z - targetPosition.Z;
            if (MathF.Sqrt(dx * dx + dy * dy + dz * dz) > 2.0f && trust.FailedRepositionAttempts < 3)
            {
                path.PathTo(targetPosition.X, targetPosition.Y, targetPosition.Z, PathFlag.Run);
            }
            else
            {
                FaceTarget(trust, target);
                trust.InTransit = false;
            }
        }

        // CTrustController::GetPartyPosition: its place among the master's trusts
        private static int TrustPartyPosition(CharData master, TrustEntity trust)
        {
            int i = master.PTrusts.IndexOf(trust);
            return i < 0 ? 0 : i;
        }

        // CTrustController::DoRoamTick: engaging the master's target once the master fights (and swung in the last
        // second, unless the charvar TrustEngageType is 1; never Cornelia); in formation behind the master (the first
        // trust) or the trust ahead of it (moving off when too close, closing in when too far, a warp beyond 30 yalms,
        // stopping in range), and the rest's 5% HP and MP away from fights
        private void TrustRoamTick(TrustEntity trust, CharData master, long now)
        {
            bool masterMeleeSwing = master.LastAttackMs > now - 1000;
            bool trustEngageCondition = CharVar(master, "TrustEngageType") switch
            {
                // Master engages a monster, no melee swing required
                1 => master.BattleTarget != null,
                // Default retail behavior: Master engages a monster and executes a melee swing
                _ => master.BattleTarget != null && masterMeleeSwing,
            };
            if (master.AttackTarget != null && trustEngageCondition && trust.Look[1] != ModelIdCornelia)
                TrustEngage(trust, master.BattleTarget, now);

            int partyPos = TrustPartyPosition(master, trust);
            BattleEntity followTarget = partyPos > 0 ? master.PTrusts[partyPos - 1] : master;
            float currentDistance = Distance(trust, followTarget);

            bool isFirstTrust = partyPos == 0;
            float declumpDistance = isFirstTrust ? 1.0f : 1.5f; // Too close, need to move away
            float followMax = isFirstTrust ? 2.0f : 3.5f;       // Maximum follow distance before moving closer
            float followTarget2 = isFirstTrust ? 1.5f : 3.0f;   // Ideal follow distance

            var path = PathOf(trust);
            if (currentDistance < declumpDistance)
            {
                // Too close to follow target - push away to maintain formation spacing
                byte away = WorldAngle(followTarget.X, followTarget.Z, followTarget.Rotation, trust.X, trust.Z);
                float total = ZoneLine.RotationToRadian(away);
                float d = followTarget2 + 0.5f;
                float x = followTarget.X + MathF.Cos(2 * MathF.PI - total) * d, z = followTarget.Z + MathF.Sin(2 * MathF.PI - total) * d;
                if (path.ValidPosition(x, followTarget.Y, z, now) && path.PathTo(x, followTarget.Y, z, PathFlag.Run)) path.FollowPath(now);
            }
            else if (currentDistance > followMax)
            {
                if (currentDistance > TrustWarpDistance)
                {
                    // CPathFind::WarpTo
                    path.Clear();
                    (trust.X, trust.Y, trust.Z) = NearPosition(followTarget, 2.0f, MathF.PI);
                    trust.Rotation = WorldAngle(trust.X, trust.Z, trust.Rotation, followTarget.X, followTarget.Z);
                    trust.UpdateMask |= EntityUpdatePacket.UpdatePos;
                }
                else if (currentDistance < TrustRoamDistance * 3.0f &&
                         path.PathAround(followTarget.X, followTarget.Y, followTarget.Z, followTarget2, PathFlag.Run))
                {
                    path.FollowPath(now);
                }
                else if (trust.Speed > 0)
                {
                    path.StepTo(followTarget.X, followTarget.Y, followTarget.Z, true);
                }
            }
            else if (path.IsFollowingPath)
            {
                // In formation range - stop pathfinding to prevent circling
                path.Clear();
            }

            // CBattleEntity::CanRest, then the rest after 15 s away from fights and the tick delay
            bool canRest = trust.GetMod(Mod.REGEN_DOWN) == 0 && !trust.StatusEffects.HasStatusEffectByFlag(EffectFlag.NoRest);
            if (canRest && now - trust.LastAttackedMs > TrustHealTickDelaysMs[0] && now - trust.CombatEndMs > TrustHealTickDelaysMs[0] &&
                now - trust.LastHealTickMs > TrustHealTickDelaysMs[trust.NumHealingTicks])
            {
                if (trust.Hp != trust.MaxHp || trust.Mp != trust.MaxMp)
                {
                    // recover 5% HP & MP
                    MobAddHp(trust, (int)(trust.MaxHp * 0.05));
                    BattleAddMp(trust, (int)(trust.MaxMp * 0.05));
                    trust.LastHealTickMs = now;
                    trust.UpdateMask |= EntityUpdatePacket.UpdateHp;
                    trust.NumHealingTicks = Math.Clamp(trust.NumHealingTicks + 1, 0, TrustHealTickDelaysMs.Length - 1);
                }
            }
        }

        // ---- party packets -----------------------------------------------------------------------------------------

        // GP_SERV_COMMAND_GROUP_LIST (0x0DD) for a trust: its numbers and jobs, no flags, the packet name cut to the
        // script name's length
        private static BasicPacket TrustGroupList(TrustEntity trust, byte memberNumber)
        {
            var w = new PacketWriter(0x0DD, 60);
            w.U32(4, trust.Id);
            w.I32(8, trust.Hp);
            w.I32(12, trust.Mp);
            w.I32(16, trust.Tp);
            w.U16(24, trust.TargId);
            w.U8(26, memberNumber);
            w.U8(29, trust.Hpp);
            w.U8(30, trust.Mpp);
            w.U8(34, trust.MJob);
            w.U8(35, trust.MLvl);
            w.U8(36, trust.SJob);
            w.U8(37, trust.SLvl);
            int len = Math.Min(trust.Name.Length, 16);
            for (int i = 0; i < len && i < trust.PacketName.Length; i++) w.U8(40 + i, trust.PacketName[i]);
            w.SetSize((uint)(4 + 52 - 16 + ((len + 3) & ~3) + 4));
            return w.Packet;
        }

        // GP_SERV_COMMAND_GROUP_ATTR (0x0DF) for a trust
        private static BasicPacket TrustGroupAttr(TrustEntity trust)
        {
            var w = new PacketWriter(0x0DF, 40);
            w.U32(4, trust.Id);
            w.I32(8, trust.Hp);
            w.I32(12, trust.Mp);
            w.I32(16, trust.Tp);
            w.U16(20, trust.TargId);
            w.U8(22, trust.Hpp);
            w.U8(23, trust.Mpp);
            w.U8(32, trust.MJob);
            w.U8(33, trust.MLvl);
            w.U8(34, trust.SJob);
            w.U8(35, trust.SLvl);
            return w.Packet;
        }

        // spell::CanUseSpell for a trust: not hidden or untargetable, and at the level its spell list gives the spell
        private static bool TrustCanUseSpell(TrustEntity trust, Spell spell)
        {
            if (spell == null) return false;
            if ((trust.NameVis & MobEntity.NameVisHideName) != 0 || trust.Untargetable) return false;
            return trust.MLvl >= (trust.SpellList?.GetSpellMinLevel(spell.Id) ?? 255);
        }

        // ---- bindings ----------------------------------------------------------------------------------------------

        private void RegisterTrustBindings(Dictionary<string, LuaFunction> m)
        {
            // CLuaBaseEntity::spawnTrust: the trust, or nil
            m["spawnTrust"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    l.PushNil();
                    return 1;
                }
                l.PushObject(SpawnTrust(c, (uint)l.ToIntArg(2)));
                return 1;
            };
            m["clearTrusts"] = l =>
            {
                if (Entity(l) is CharData c) ClearTrusts(c);
                else log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                return 0;
            };
            m["getTrustID"] = l =>
            {
                if (Entity(l) is TrustEntity t) return Ret(l, t.TrustId);
                log_($"Invalid Entity calling function ({NameOf(Entity(l))}).");
                return Ret(l, 0);
            };
            // CLuaBaseEntity::trustPartyMessage: a BATTLE_MESSAGE2 (711) from the trust to each of its master's party
            m["trustPartyMessage"] = l =>
            {
                if (Entity(l) is not TrustEntity t)
                {
                    log_($"Invalid Entity calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                if (t.PMaster is CharData master)
                {
                    int messageId = l.ToIntArg(2);
                    foreach (var member in master.Party?.Members.ToArray() ?? new[] { master })
                        PushTo(member, SelfPackets.BattleMessage2(t, member, messageId, 0, MsgTrustPartyMessage));
                }
                return 0;
            };
            m["getPartyLastMemberJoinedTime"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return Ret(l, 0);
                }
                if (c.Party == null) return Ret(l, 0);
                // CParty::GetTimeLastMemberJoined: when a member last joined the leader's party, else now (the steady
                // clock), as earth_time::timestamp(timer::to_utc(...)): the Earth time that far before now, on the
                // wall clock GetSystemTime reads (the script engine's), so the two always agree
                long joined = c.Party.Leader is CharData leader ? leader.LeaderCreatedPartyTime : NowMs();
                return Ret(l, (uint)Math.Floor((scripts_.UnixNowMs() - (NowMs() - joined)) / 1000.0));
            };
            // CLuaBaseEntity::addGambit(target, predicates, reactions, retry): the gambit's id, "" when a specific
            // spell is beyond the trust
            m["addGambit"] = l =>
            {
                if (Entity(l) is not TrustEntity t)
                {
                    log_($"Invalid Entity calling function ({NameOf(Entity(l))}).");
                    l.PushString("");
                    return 1;
                }
                var g = new TrustGambit { Target = (ushort)l.ToIntArg(2) };
                ReadGambitPredicates(l, 3, g);
                ReadGambitReactions(l, 4, g);
                g.RetryDelaySec = l.IsNil(5) ? (ushort)0 : (ushort)l.ToIntArg(5);
                foreach (var (reaction, select, argument) in g.Actions)
                {
                    if (reaction == GReactionMa && select == GSelectSpecific && !TrustCanUseSpell(t, GetSpell((ushort)argument)))
                    {
                        l.PushString("");
                        return 1;
                    }
                }
                // CGambitsContainer::NewGambitIdentifier
                g.Id = $"{t.Gambits.Count}_{g.PredicateGroups.Count}_{g.Actions.Count}";
                t.Gambits.Add(g);
                l.PushString(g.Id);
                return 1;
            };
            m["removeGambit"] = l =>
            {
                if (Entity(l) is TrustEntity t)
                {
                    string id = l.ToStringValue(2);
                    t.Gambits.RemoveAll(g => g.Id == id);
                }
                else log_($"Invalid Entity calling function ({NameOf(Entity(l))}).");
                return 0;
            };
            m["removeAllGambits"] = l =>
            {
                if (Entity(l) is TrustEntity t) t.Gambits.Clear();
                else log_($"Invalid Entity calling function ({NameOf(Entity(l))}).");
                return 0;
            };
            m["setTrustTPSkillSettings"] = l =>
            {
                if (Entity(l) is not TrustEntity t)
                {
                    log_($"Invalid Entity calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                t.TpTrigger = (ushort)l.ToIntArg(2);
                t.TpSelect = (ushort)l.ToIntArg(3);
                t.TpValue = l.IsNil(4) ? (ushort)0 : (ushort)l.ToIntArg(4);
                return 0;
            };
        }


        // addGambit's extractPredicates: { cond, arg }, a table of them, or ai.l logic wrappers ({ logic, conditions });
        // a wrapped group goes in twice, as in the C++
        private static void ReadGambitPredicates(LuaState l, int idx, TrustGambit g)
        {
            if (!l.IsTable(idx)) return;
            l.RawGetI(idx, 1);
            bool nested = l.IsTable(-1);
            l.Pop(1);
            if (nested)
            {
                int n = (int)l.ObjLen(idx);
                for (int i = 1; i <= n; i++)
                {
                    l.RawGetI(idx, i);
                    int entry = l.Top;
                    var preds = new List<(ushort, uint)>();
                    ushort logic = 0; // G_LOGIC::AND
                    l.GetField(entry, "logic");
                    bool wrapped = !l.IsNil(-1);
                    if (wrapped) logic = (ushort)l.ToIntegral(-1);
                    l.Pop(1);
                    if (wrapped)
                    {
                        l.GetField(entry, "conditions");
                        int conds = l.Top;
                        int cn = l.IsTable(conds) ? (int)l.ObjLen(conds) : 0;
                        for (int k = 1; k <= cn; k++)
                        {
                            l.RawGetI(conds, k);
                            preds.Add(ReadPair(l, l.Top));
                            l.Pop(1);
                        }
                        l.Pop(1);
                        g.PredicateGroups.Add((logic, preds));
                    }
                    else preds.Add(ReadPair(l, entry));
                    g.PredicateGroups.Add((logic, preds));
                    l.Pop(1);
                }
            }
            else if (l.ObjLen(idx) == 2)
            {
                g.PredicateGroups.Add((0, new List<(ushort, uint)> { ReadPair(l, idx) }));
            }
        }

        private static (ushort, uint) ReadPair(LuaState l, int idx)
        {
            l.RawGetI(idx, 1);
            ushort a = (ushort)l.ToIntegral(-1);
            l.RawGetI(idx, 2);
            uint b = (uint)l.ToIntegral(-1);
            l.Pop(2);
            return (a, b);
        }

        // addGambit's extractReactions: { reaction, select, arg } or a table of them
        private static void ReadGambitReactions(LuaState l, int idx, TrustGambit g)
        {
            if (!l.IsTable(idx)) return;
            (ushort, ushort, uint) Read(int t)
            {
                l.RawGetI(t, 1);
                l.RawGetI(t, 2);
                l.RawGetI(t, 3);
                var r = ((ushort)l.ToIntegral(-3), (ushort)l.ToIntegral(-2), (uint)l.ToIntegral(-1));
                l.Pop(3);
                return r;
            }
            l.RawGetI(idx, 1);
            bool nested = l.IsTable(-1);
            l.Pop(1);
            if (nested)
            {
                int n = (int)l.ObjLen(idx);
                for (int i = 1; i <= n; i++)
                {
                    l.RawGetI(idx, i);
                    g.Actions.Add(Read(l.Top));
                    l.Pop(1);
                }
            }
            else if (l.ObjLen(idx) == 3) g.Actions.Add(Read(idx));
        }
    }
}
