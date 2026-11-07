using System;
using System.Collections.Generic;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // Mob pets: a mob's pet is another mob of its zone that setPet linked to it (xi.pet.setMobPet), spawned beside its
    // master by spawnPet. petutils' mob half (SpawnMobPet with its pet_list row, AttackTarget, RetreatToMaster,
    // DespawnPet refusing a mob master), the bindings (hasPet, spawnPet, despawnPet, petAttack, petRetreat, getPetID,
    // getPetName, setPetStats, add / del / setPetMod with the master's pet mods and its gear's item_mods_pet) and
    // CMobController's special skill (IsSpecialSkillReady / TrySpecialSkill: a BST mob's
    // Call Beast and the like). The controller's pet handling (the pet engaging with its master, the master's help,
    // the pet shadowing its roaming master, a dead master's pet despawning, an SMN casting only without a pet) is in
    // ZoneHost.MobAi.cs, ZoneHost.MobLink.cs and ZoneHost.MobRoam.cs.
    // A char's spawnPet / despawnPet go to the player pets (ZoneHost.PlayerPets.cs), a charmed mob is
    // ZoneHost.Charm.cs's and the avatar bodyguard ZoneHost.MobLink.cs's.
    public sealed partial class ZoneHost
    {
        private const int PetIdDarkSpirit = 7;

        // Pet_t: a pet_list row with its pool, the pool's species and its resistances. The HP and MP scales are never
        // filled in (0), as in the C++.
        private sealed class PetInfo
        {
            public ushort Id;
            public string Name = "";
            public ushort[] Look = new ushort[10];
            public ushort Species;
            public byte Ecosystem, MJob, SJob, Element;
            public ushort SpellList;
            public bool HasSpellScript;
            public readonly List<(Mod Mod, short Value)> Mods = new List<(Mod, short)>();
            public byte MinLevel, MaxLevel;
            public uint TimeSec;
            public byte ModelSize;
            public float ModelHitboxSize;
            public byte Speed;
            public byte StrRank, DexRank, VitRank, AgiRank, IntRank, MndRank, ChrRank, AttRank, DefRank, EvaRank, AccRank;
            public byte CmbSkill, NamePrefix, DmgType;
            public ushort CmbDelay, MobSkillList;
            public float HpScale = 1.0f, MpScale = 1.0f; // Pet_t's HPscale / MPscale: 1 (never loaded)
        }

        private Dictionary<int, PetInfo> petList_;
        private Dictionary<ushort, SpeciesInfo> speciesById_;

        // petutils::LoadPetList: pet_list joined to its pool and the pool's resistances
        private Dictionary<int, PetInfo> PetList()
        {
            if (petList_ != null) return petList_;
            petList_ = new Dictionary<int, PetInfo>();
            if (ecosystems_ != null && speciesById_ == null)
            {
                speciesById_ = new Dictionary<ushort, SpeciesInfo>();
                foreach (var sp in ecosystems_.SpeciesByName.Values) speciesById_[sp.Id] = sp;
            }
            var r = Exec("SELECT pet_list.petid, pet_list.name, mob_pools.modelid, minLevel, maxLevel, time, mob_pools.speciesid, " +
                         "mob_pools.mJob, mob_pools.sJob, mob_pools.cmbSkill, pet_list.element, hasSpellScript, mob_pools.spellList, cmbDelay, " +
                         "name_prefix, mob_pools.skill_list_id, damageType, mob_pools.modelSize, mob_pools.modelHitboxSize, slash_sdt, pierce_sdt, h2h_sdt, impact_sdt, magical_sdt, fire_sdt, ice_sdt, wind_sdt, earth_sdt, " +
                         "lightning_sdt, water_sdt, light_sdt, dark_sdt, fire_res_rank, ice_res_rank, wind_res_rank, earth_res_rank, " +
                         "lightning_res_rank, water_res_rank, light_res_rank, dark_res_rank, paralyze_res_rank, bind_res_rank, " +
                         "silence_res_rank, slow_res_rank, poison_res_rank, light_sleep_res_rank, dark_sleep_res_rank, blind_res_rank, " +
                         "stun_res_rank, gravity_res_rank FROM pet_list, mob_pools, mob_resistances " +
                         "WHERE pet_list.poolid = mob_pools.poolid AND mob_resistances.resist_id = mob_pools.resist_id");
            while (r != null && r.Next())
            {
                var pet = new PetInfo
                {
                    Id = (ushort)r.Get<uint>("petid"),
                    Name = r.Get<string>("name"),
                    Species = r.Get<ushort>("speciesid"),
                    MJob = r.Get<byte>("mJob"),
                    Element = r.Get<byte>("element"),
                    SpellList = (ushort)r.Get<short>("spellList"),
                    SJob = r.Get<byte>("sJob"),
                    HasSpellScript = r.Get<byte>("hasSpellScript") != 0,
                    MinLevel = r.Get<byte>("minLevel"),
                    MaxLevel = r.Get<byte>("maxLevel"),
                    TimeSec = r.Get<uint>("time"),
                    ModelSize = r.IsNull("modelSize") ? (byte)0 : r.Get<byte>("modelSize"),
                    ModelHitboxSize = Math.Max(0.0f, (r.IsNull("modelHitboxSize") ? 0 : r.Get<byte>("modelHitboxSize")) / 10.0f),
                    CmbSkill = r.Get<byte>("cmbSkill"),
                    CmbDelay = r.Get<ushort>("cmbDelay"),
                    NamePrefix = r.Get<byte>("name_prefix"),
                    MobSkillList = r.Get<ushort>("skill_list_id"),
                    DmgType = r.Get<byte>("damageType"),
                };
                if (!r.IsNull("modelid"))
                {
                    var look = r.GetBlobBytes("modelid");
                    for (int i = 0; i < 10 && 2 * i + 1 < look.Length; i++) pet.Look[i] = (ushort)(look[2 * i] | look[2 * i + 1] << 8);
                }
                // the species' ecosystem, speed and stat ranks (mobutils::ApplyStatRanks)
                SpeciesInfo species = null;
                speciesById_?.TryGetValue(pet.Species, out species);
                if (species != null) pet.Ecosystem = species.Ecosystem;
                var a = species?.Attributes ?? new MobAttributes();
                pet.Speed = a.SpeedOrDefault;
                pet.StrRank = a.StrRank; pet.DexRank = a.DexRank; pet.VitRank = a.VitRank; pet.AgiRank = a.AgiRank;
                pet.IntRank = a.IntRank; pet.MndRank = a.MndRank; pet.ChrRank = a.ChrRank;
                pet.DefRank = a.DefRank; pet.EvaRank = a.EvaRank; pet.AttRank = a.AttRank; pet.AccRank = a.AccRank;
                foreach (var (column, mod) in PetResistColumns)
                    pet.Mods.Add((mod, r.Get<short>(column)));
                petList_[pet.Id] = pet;
            }
            return petList_;
        }

        private static readonly (string Column, Mod Mod)[] PetResistColumns =
        {
            ("slash_sdt", Mod.SLASH_SDT), ("pierce_sdt", Mod.PIERCE_SDT), ("h2h_sdt", Mod.HTH_SDT), ("impact_sdt", Mod.IMPACT_SDT),
            ("magical_sdt", Mod.UDMGMAGIC),
            ("fire_sdt", Mod.FIRE_SDT), ("ice_sdt", Mod.ICE_SDT), ("wind_sdt", Mod.WIND_SDT), ("earth_sdt", Mod.EARTH_SDT),
            ("lightning_sdt", Mod.THUNDER_SDT), ("water_sdt", Mod.WATER_SDT), ("light_sdt", Mod.LIGHT_SDT), ("dark_sdt", Mod.DARK_SDT),
            ("fire_res_rank", Mod.FIRE_RES_RANK), ("ice_res_rank", Mod.ICE_RES_RANK), ("wind_res_rank", Mod.WIND_RES_RANK),
            ("earth_res_rank", Mod.EARTH_RES_RANK), ("lightning_res_rank", Mod.THUNDER_RES_RANK), ("water_res_rank", Mod.WATER_RES_RANK),
            ("light_res_rank", Mod.LIGHT_RES_RANK), ("dark_res_rank", Mod.DARK_RES_RANK),
            ("paralyze_res_rank", Mod.PARALYZE_RES_RANK), ("bind_res_rank", Mod.BIND_RES_RANK), ("silence_res_rank", Mod.SILENCE_RES_RANK),
            ("slow_res_rank", Mod.SLOW_RES_RANK), ("poison_res_rank", Mod.POISON_RES_RANK), ("light_sleep_res_rank", Mod.LIGHT_SLEEP_RES_RANK),
            ("dark_sleep_res_rank", Mod.DARK_SLEEP_RES_RANK), ("blind_res_rank", Mod.BLIND_RES_RANK), ("stun_res_rank", Mod.STUN_RES_RANK),
            ("gravity_res_rank", Mod.GRAVITY_RES_RANK),
        };

        // petutils::SpawnMobPet: a mob SMN's elemental or avatar takes the pet_list row's look, name, job, kind, spell
        // list and resistances (the SDT and resistance mods saved, so a respawn's restore keeps them). Pet_t's HP and
        // MP scales are never loaded: the Pet_t constructor's 1.
        private void SpawnMobPet(BattleEntity master, int petId, bool preserveName)
        {
            if (!PetList().TryGetValue(petId, out var petData))
            {
                log_($"petutils::SpawnMobPet: no pet_list row {petId}");
                return;
            }
            if (master.PPet is not MobEntity pet) return;
            pet.Look = (ushort[])petData.Look.Clone();
            if (!preserveName) pet.Name = petData.Name;
            pet.MJob = petData.MJob;
            pet.Ecosystem = petData.Ecosystem;
            pet.Species = petData.Species;
            pet.Element = petData.Element;
            pet.MpScale = petData.MpScale;
            pet.Allegiance = master switch
            {
                MobEntity mm => mm.Allegiance,
                CharData c => c.Allegiance,
                _ => pet.Allegiance,
            };
            foreach (var e in master.StatusEffects.All)
                if (!e.Deleted && e.HasFlag(EffectFlag.Confrontation)) pet.StatusEffects.AddStatusEffect(e.Copy());
            if (pet.Ecosystem == Ecosystem.Elemental) pet.SetModifier(Mod.DMGPHYS, -5000); // assuming elemental spawn: -50% PDT
            pet.SpellList = GetMobSpellList(petData.SpellList);
            foreach (var (mod, value) in petData.Mods) pet.SetModifier(mod, value);
            pet.SavePetModifiers(PetResistMods);
        }

        private static readonly Mod[] PetResistMods = Array.ConvertAll(PetResistColumns, c => c.Mod);

        // battleutils::nearPosition: offset yalms from the point, at the angle turned from its rotation
        private static (float X, float Y, float Z) NearPosition(BattleEntity e, float offset, float radian)
        {
            float total = ZoneLine.RotationToRadian(e.Rotation) + radian;
            return (e.X + MathF.Cos(2 * MathF.PI - total) * offset, e.Y, e.Z + MathF.Sin(2 * MathF.PI - total) * offset);
        }

        // petutils::AttackTarget: the pet engages unless something keeps it from acting
        private void PetAttackTarget(BattleEntity master, BattleEntity target)
        {
            if (master?.PPet == null || target == null)
            {
                log_("petutils::AttackTarget() - Null master, pet, or target passed to function.");
                return;
            }
            if (master.PPet is not MobEntity pet || pet.StatusEffects.HasPreventActionEffect(false)) return;
            if (pet is PetEntity || pet.PMaster is CharData)
            {
                if (target is MobEntity mob) PetEngage(pet, mob, NowMs());
            }
            else MobEngage(pet, target, NowMs());
        }

        // ---- the special skill (CMobController::IsSpecialSkillReady / TrySpecialSkill) ------------------------------

        private bool IsSpecialSkillReady(MobEntity m, float currentDistance, long now)
        {
            if (m.GetMobMod(MobMod.SpecialSkill) == 0) return false;
            if (m.StatusEffects.HasStatusEffect(EffectChainspell)) return false;
            // mobs use ranged attacks quicker when standing back
            int bonusTime = currentDistance > 5 ? m.GetMobMod(MobMod.StandbackCool) : 0;
            return now >= m.LastSpecialMs + (m.GetMobMod(MobMod.SpecialCool) - bonusTime) * 1000L;
        }

        private const ushort EffectChainspell = 48;

        private bool TrySpecialSkill(MobEntity m, long now)
        {
            var target = m.BattleTarget;
            var skill = GetMobSkill(m.GetMobMod(MobMod.SpecialSkill));
            if (skill == null)
            {
                log_($"CAIMobDummy::ActionSpawn Special skill was set but not found! ({m.GetMobMod(MobMod.SpecialSkill)})");
                return false;
            }
            if (!m.MobAbilityEnabled) return false;
            if (m.Hidden && (m.NameVis & MobEntity.NameVisHideName) == 0) return false;
            // self-targeted skills hit the mob, otherwise the target must be in range
            BattleEntity abilityTarget = (skill.ValidTargets & TargetSelf) != 0 ? m
                : target != null && Distance(m, target) <= skill.Distance ? target : null;
            if (abilityTarget == null) return false;
            if (OnMobSkillCheck(abilityTarget, m, skill) != 0) return false;
            if (!MobUseSkill(m, abilityTarget, skill.Id, null, now)) return false;
            m.LastSpecialMs = now;
            return true;
        }

        // ---- the master's pet mods (CBattleEntity::add / set / delPetModifier) -----------------------------------------

        private const byte PetModAll = 0;

        // petutils::CheckPetModType: All applies to every pet and every type to a pet that is not a CPetEntity; a
        // CPetEntity takes its own type's (an automaton the Automaton type's and its frame's)
        private static bool CheckPetModType(BattleEntity pet, byte petModType)
        {
            if (petModType == PetModAll) return true;
            if (pet is not PetEntity p) return true;
            return petModType == PetModAvatar && p.PetType == PetType.Avatar || petModType == PetModWyvern && p.PetType == PetType.Wyvern ||
                   petModType >= PetModAutomaton && petModType <= PetModStormwaker && p is AutomatonEntity automaton &&
                   (petModType == PetModAutomaton || petModType + 28 == automaton.Frame) ||
                   petModType == PetModLuopan && p.PetType == PetType.Luopan;
        }

        // the master keeps the total; a pet out now gets the change at once
        internal static void AddPetModifier(BattleEntity master, Mod mod, byte petModType, short amount)
        {
            master.PetMods.TryGetValue((petModType, mod), out short current);
            master.PetMods[(petModType, mod)] = (short)(current + amount);
            if (master.PPet != null && CheckPetModType(master.PPet, petModType))
            {
                master.PPet.AddModifier(mod, amount);
                CharStats.UpdateHealth(master.PPet);
            }
        }

        private static void SetPetModifier(BattleEntity master, Mod mod, byte petModType, short amount)
        {
            master.PetMods[(petModType, mod)] = amount;
            if (master.PPet != null && CheckPetModType(master.PPet, petModType))
            {
                master.PPet.SetModifier(mod, amount);
                CharStats.UpdateHealth(master.PPet);
            }
        }

        // addPetMod / delPetMod: the master's all-pets mod, up or down
        private int PetModBinding(LuaState l, int sign)
        {
            if (Entity(l) is not BattleEntity b)
            {
                log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                return 0;
            }
            AddPetModifier(b, (Mod)(ushort)l.ToIntArg(2), PetModAll, (short)(sign * (short)l.ToIntArg(3)));
            return 0;
        }

        // ---- the bindings -------------------------------------------------------------------------------------------

        private void RegisterMobPetBindings(Dictionary<string, LuaFunction> m)
        {
            m["hasPet"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                    return Ret(l, false);
                }
                return Ret(l, b.PPet != null && !(b.PPet is MobEntity pm && !pm.IsSpawned));
            };
            // a mob master's pet spawns 2.2 yalms behind it (taking a pet_list row's stats first when given an id)
            m["spawnPet"] = l =>
            {
                switch (Entity(l))
                {
                    case CharData c:
                        if (l.IsNumber(2)) SpawnPet(c, l.ToIntArg(2));
                        else log_("CLuaBaseEntity::spawnPet : PetID is nullptr");
                        break;
                    case MobEntity mob:
                        if (mob.PPet is not MobEntity pet)
                        {
                            log_($"lua_baseentity::spawnPet PMob ({mob.Id}) trying to spawn pet but its nullptr");
                            break;
                        }
                        if (l.IsNumber(2)) SpawnMobPet(mob, l.ToIntArg(2), false);
                        (pet.SpawnX, pet.SpawnY, pet.SpawnZ) = NearPosition(mob, 2.2f, MathF.PI);
                        pet.SpawnRotation = mob.Rotation;
                        SpawnMob(AreaOf(pet), pet);
                        break;
                    default:
                        log_("CLuaBaseEntity::spawnPet() - NPC calling function.");
                        break;
                }
                return 0;
            };
            // petutils::DespawnPet -> DetachPet, which only a char's pet goes through
            m["despawnPet"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                    return 0;
                }
                if (b.PPet == null) return 0;
                if (b is CharData c) DespawnPet(c);
                else log_($"Non-PC passed into function ({b.Name})");
                return 0;
            };
            m["petAttack"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                    return 0;
                }
                if (b.PPet != null) PetAttackTarget(b, l.ToObject(2) as BattleEntity);
                return 0;
            };
            // petutils::RetreatToMaster: the pet disengages
            m["petRetreat"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                    return 0;
                }
                if (b.PPet is MobEntity pet && pet.Engaged) MobDisengage(pet, NowMs());
                return 0;
            };
            m["addPetMod"] = l => PetModBinding(l, 1);
            m["delPetMod"] = l => PetModBinding(l, -1);
            m["setPetMod"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                    return 0;
                }
                SetPetModifier(b, (Mod)(ushort)l.ToIntArg(2), PetModAll, (short)l.ToIntArg(3));
                return 0;
            };
            // the pet's element (m_Element)
            m["getPetElement"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid Entity ({NameOf(Entity(l))}) calling function.");
                    return Ret(l, 0);
                }
                return Ret(l, b.PPet is MobEntity pet ? pet.Element : 0);
            };
            // a CPetEntity's petID; 0 for any other entity
            m["getPetID"] = l => Ret(l, Entity(l) is PetEntity p ? p.PetId : 0);
            // hasJugPet: a char's pet out (not disappearing) that is a jug pet
            m["hasJugPet"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid Entity ({NameOf(Entity(l))}) calling function.");
                    return Ret(l, false);
                }
                return Ret(l, c.PPet is PetEntity p && p.Status != NpcEntity.StatusDisappear && p.PetType == PetType.JugPet);
            };
            // setJugRemainingTime(seconds): the jug's duration made the time it has lived plus the seconds given
            m["setJugRemainingTime"] = l =>
            {
                if (Entity(l) is not PetEntity { PetType: PetType.JugPet } jug)
                {
                    log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                    return 0;
                }
                long previousJugLifetime = NowMs() - jug.JugSpawnMs;
                jug.JugDurationMs = previousJugLifetime + l.ToUIntArg(2) * 1000L;
                return 0;
            };
            m["getPetName"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                    l.Push("");
                    return 1;
                }
                if (b.PPet == null) log_("PPet does not exist.");
                l.Push(b.PPet?.Name ?? "");
                return 1;
            };
            // the pet's onMobSpawn: a spirit's model, job, spells and stats, keeping its name
            m["setPetStats"] = l =>
            {
                int petId = l.ToIntArg(2);
                if (Entity(l) is not MobEntity pet || pet.PMaster == null || pet.PMaster.PPet != pet || petId > PetIdDarkSpirit)
                {
                    log_("setPetStats: expected a linked mob pet and a spirit ID.");
                    return 0;
                }
                SpawnMobPet(pet.PMaster, petId, true);
                pet.TraitList.Clear();
                MobStatsFor().CalculateMobStats(pet, true, ZoneInfoOf(pet.Zone).Types.Contains("instanced"));
                ResolveMobSpells(pet);
                return 0;
            };
        }
    }
}
