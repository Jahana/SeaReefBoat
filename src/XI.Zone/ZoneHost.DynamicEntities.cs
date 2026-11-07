using System;
using System.Collections.Generic;
using System.Globalization;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // Dynamic entities:
    // - luautils::GenerateDynamicEntity (CLuaZone::insertDynamicEntity): an NPC, or a mob from mob_groups
    //   (mobutils::InstantiateDynamicMob), under a dynamic targid, named DE_<name>, with the table's functions cached
    //   at xi.zones.<zone>.<npcs|mobs>.DE_<name> (where the C# script lookups find them).
    // - mobutils::InstantiateAlly: a mob_groups ally for CLuaBattlefield::insertEntity(ally) and
    //   CLuaBaseEntity::instantiateMob.
    // - CZoneEntities' mobTick / npcTick deletions: a dynamic entity that disappears leaves the zone, and its targid
    //   is freed 60 s later.
    // A dynamic NPC given an onTrigger is flagged triggerable (CNpcEntity::setTriggerable). An instance's
    // insertDynamicEntity / insertAlly are ZoneHost.Instances.cs's.
    public sealed partial class ZoneHost
    {
        // the mob_groups columns InstantiateDynamicMob reads (InstantiateAlly adds the spawn point's levels and the
        // status resistance ranks)
        private const string DynamicMobColumns =
            "zoneid, mob_groups.name, packet_name, respawntime, spawntype, dropid, mob_groups.HP, mob_groups.MP, modelid, mJob, sJob, cmbSkill, " +
            "cmbDmgMult, cmbDelay, behavior, links, mobType, immunity, slash_sdt, pierce_sdt, h2h_sdt, impact_sdt, magical_sdt, fire_sdt, " +
            "ice_sdt, wind_sdt, earth_sdt, lightning_sdt, water_sdt, light_sdt, dark_sdt, fire_res_rank, ice_res_rank, wind_res_rank, " +
            "earth_res_rank, lightning_res_rank, water_res_rank, light_res_rank, dark_res_rank, mob_pools.speciesid, name_prefix, " +
            "entityFlags, animationsub, hasSpellScript, spellList, mob_groups.poolid, allegiance, namevis, aggro, mob_pools.modelSize, " +
            "mob_pools.modelHitboxSize, mob_pools.skill_list_id, mob_pools.true_detection";

        // PetResistColumns' first 21: the SDTs and the elemental resistance ranks (InstantiateDynamicMob has no status ranks)
        private const int ElementalResistColumns = 21;

        // the shared part of InstantiateAlly / InstantiateDynamicMob: a new mob from its mob_groups row, null without one
        private MobEntity InstantiateSqlMob(uint groupId, ushort groupZoneId, bool ally)
        {
            string sql = ally
                ? "SELECT " + DynamicMobColumns + ", mob_spawn_points.minLevel, mob_spawn_points.maxLevel, paralyze_res_rank, bind_res_rank, " +
                  "silence_res_rank, slow_res_rank, poison_res_rank, light_sleep_res_rank, dark_sleep_res_rank, blind_res_rank, stun_res_rank, " +
                  "gravity_res_rank FROM mob_groups INNER JOIN mob_spawn_points ON mob_groups.groupid = mob_spawn_points.groupid " +
                  "INNER JOIN mob_pools ON mob_groups.poolid = mob_pools.poolid INNER JOIN mob_resistances ON mob_pools.resist_id = mob_resistances.resist_id " +
                  "WHERE mob_groups.groupid = ? AND mob_groups.zoneid = ? LIMIT 1"
                : "SELECT " + DynamicMobColumns + " FROM mob_groups INNER JOIN mob_pools ON mob_groups.poolid = mob_pools.poolid " +
                  "INNER JOIN mob_resistances ON mob_pools.resist_id = mob_resistances.resist_id " +
                  "WHERE mob_groups.groupid = ? AND mob_groups.zoneid = ? LIMIT 1";
            var r = Exec(sql, BoundValue.Of(groupId), BoundValue.Of(groupZoneId));
            if (r == null || !r.Next()) return null;

            ecosystems_ ??= Ecosystems.Load(dataDir_, enums_);
            PetList(); // the species by id
            var m = new MobEntity
            {
                Name = r.Get<string>("name"),
                PacketName = r.Get<string>("packet_name") ?? "",
                RespawnTimeSec = r.Get<uint>("respawntime"),
                SpawnType = r.Get<ushort>("spawntype"),
                DropId = r.Get<uint>("dropid"),
                HpModifier = r.Get<uint>("HP"),
                MpModifier = r.Get<uint>("MP"),
                MJob = r.Get<byte>("mJob"),
                SJob = r.Get<byte>("sJob"),
                WeaponSkill = r.Get<byte>("cmbSkill"),
                DamageMultiplier = r.Get<ushort>("cmbDmgMult"),
                Behavior = r.Get<ushort>("behavior"),
                Type = r.Get<ushort>("mobType"),
                Immunity = r.Get<uint>("immunity"),
                Species = r.Get<ushort>("speciesid"),
                NamePrefix = r.Get<byte>("name_prefix"),
            };
            if (ally)
            {
                m.MinLevel = r.Get<byte>("minLevel");
                m.MaxLevel = r.Get<byte>("maxLevel");
            }
            m.MainDelay = m.BaseDelay = r.Get<ushort>("cmbDelay");
            if (!r.IsNull("modelid"))
            {
                var look = r.GetBlobBytes("modelid");
                for (int i = 0; i < 10 && 2 * i + 1 < look.Length; i++) m.Look[i] = (ushort)(look[2 * i] | look[2 * i + 1] << 8);
            }
            AttachEffects(m);
            int resists = ally ? PetResistColumns.Length : ElementalResistColumns;
            for (int i = 0; i < resists; i++) m.SetModifier(PetResistColumns[i].Mod, r.Get<short>(PetResistColumns[i].Column));

            // ApplySpecies writes the fields it covers, so the row's own values that follow come after it
            SpeciesInfo species = null;
            speciesById_?.TryGetValue(m.Species, out species);
            if (species != null) MobStats.ApplySpecies(m, species, species.Attributes);
            if (ally) m.SetMobMod(MobMod.Charmable, 0); // Allies are never charmable

            m.EntityFlags = r.Get<uint>("entityFlags");
            m.AnimationSub = (byte)r.Get<uint>("animationsub");
            m.SpellListId = r.Get<ushort>("spellList");
            m.SpellList = GetMobSpellList(m.SpellListId);
            m.Pool = r.Get<uint>("poolid");
            m.Allegiance = r.Get<byte>("allegiance");
            m.NameVis = r.Get<byte>("namevis");
            m.ModelHitboxSize = Math.Max(0.0f, (r.IsNull("modelHitboxSize") ? 0 : r.Get<byte>("modelHitboxSize")) / 10.0f);
            m.ModelSize = r.IsNull("modelSize") ? (byte)0 : r.Get<byte>("modelSize");
            m.Aggro = r.Get<byte>("aggro") != 0;
            m.MobSkillList = r.Get<ushort>("skill_list_id");
            m.Link = r.Get<byte>("links") != 0;
            m.TrueDetection = r.Get<byte>("true_detection") != 0;
            return m;
        }

        // mobutils::AddSqlModifiers: the species' mods, then the pool's
        private void AddSqlModifiers(MobEntity m)
        {
            SpeciesInfo species = null;
            speciesById_?.TryGetValue(m.Species, out species);
            if (species?.Attributes is { } attributes)
            {
                foreach (var (mod, value) in attributes.Mods) m.AddModifier(mod, value);
                foreach (var (mod, value) in attributes.MobMods) m.SetMobMod(mod, value);
            }
            if (MobPoolMods(m.Pool) is { } poolMods)
            {
                foreach (var (modId, value, isMobMod) in poolMods)
                {
                    if (isMobMod) m.SetMobMod((MobMod)modId, value);
                    else m.AddModifier((Mod)modId, value);
                }
            }
        }

        // CZoneEntities::InsertMOB under a dynamic targid
        private void InsertDynamicMob(ZoneInfo info, MobEntity m)
        {
            info.AssignDynamicTargId(m, NowMs());
            info.Mobs.Add(m);
            info.MobById[m.Id] = m;
        }

        // luautils::OnEntityLoad, OnMobInitialize, the link party, the mixins and the saved modifiers
        private void InitDynamicMob(ZoneInfo info, MobEntity m)
        {
            if (scripts_ != null) scripts_.LoadLuaObjectFromFile(MobScriptFile(m));
            CallMobHook(m, "onMobInitialize", m);
            MobLinkFindParty(info, m);
            if (scripts_ != null)
            {
                ApplyMixins(m, MobScriptFile(m));
                ApplyMixins(m, $"./scripts/mixins/zones/{info.Name}.lua");
            }
            m.SaveModifiers();
            m.SaveMobModifiers();
        }

        // mobutils::InstantiateAlly: a mob_groups ally in the zone (or the instance run), released once it disappears
        private MobEntity InstantiateAlly(uint groupId, ushort zoneId, Instance run = null)
        {
            var m = InstantiateSqlMob(groupId, zoneId, true);
            if (m == null) return null;
            m.Instance = run;
            var info = run?.Info ?? Zone(zoneId).Info;
            InsertDynamicMob(info, m);
            // Ensure dynamic targid is released on death
            m.ReleaseTargIdOnDisappear = true;
            // must be here first to define mobmods
            MobStats.InitializeMob(m);
            InitDynamicMob(info, m);
            return m;
        }

        // luautils::GenerateDynamicEntity; an instance run's entity belongs to the run
        private object GenerateDynamicEntity(ZoneInfo info, LuaState l, int table, Instance run = null)
        {
            object entity;
            MobEntity mob = null;
            NpcEntity npc = null;
            const byte typeNpc = 0x02;
            if ((byte)FieldInt(l, table, "objtype", typeNpc) == typeNpc)
            {
                // CNpcEntity: face 0x32, no entity flags
                npc = new NpcEntity { Name = "DefaultName", EntityFlags = 0 };
                npc.Look[1] = 0x32;
                entity = npc;
            }
            else
            {
                uint groupId = (uint)FieldInt(l, table, "groupId", 0);
                ushort groupZoneId = (ushort)FieldInt(l, table, "groupZoneId", 0);
                // mobutils::InstantiateDynamicMob
                mob = InstantiateSqlMob(groupId, groupZoneId, false);
                if (mob != null)
                {
                    MobStats.InitializeMob(mob);
                    AddSqlModifiers(mob);
                }
                entity = mob;
            }
            // This can happen if the Target's Zone ID is invalid.
            if (entity == null)
            {
                log_("Failed to insert Dynamic Entity.");
                return null;
            }
            if (npc != null) npc.Instance = run;
            if (mob != null) mob.Instance = run;

            // NOTE: Mob allegiance is the default for NPCs
            // (an NPC's allegiance is not kept here)
            byte allegiance = (byte)FieldInt(l, table, "allegiance", 0);
            if (mob != null)
            {
                mob.Allegiance = allegiance;
                info.AssignDynamicTargId(mob, NowMs());
            }
            else info.AssignDynamicTargId(npc);

            byte rotation = (byte)FieldInt(l, table, "rotation", 0);
            float x = (float)FieldNumber(l, table, "x", 0.01), y = (float)FieldNumber(l, table, "y", 0.01), z = (float)FieldNumber(l, table, "z", 0.01);
            string name = FieldString(l, table, "name") ?? "";
            if (name.Length == 0)
            {
                log_($"Trying to spawn dynamic entity without a name! ({NameOf(entity)} - {info.Name})");
                // If the name hasn't been provided, use "DefaultName" for NPCs, and whatever comes from the mob_pool for Mobs
                name = NameOf(entity);
            }
            string lookupName = "DE_" + name;
            string packetName = FieldString(l, table, "packetName") ?? name;
            bool release = FieldBool(l, table, "releaseIdOnDisappear");

            // Bind any functions that are passed in, at xi.zones.<zone>.<npcs|mobs>.DE_<name>
            BindDynamicEntityFunctions(l, table, info.Name, mob != null ? "mobs" : "npcs", lookupName, mob != null);

            // Look must be set _before_ inserting the entity into the zone
            ushort[] look = mob?.Look ?? npc.Look;
            l.GetField(table, "look");
            if (l.IsNumber(-1)) look[1] = (ushort)l.ToIntegral(-1);
            else if (l.IsString(-1)) Array.Copy(StringToLook(l.ToStringValue(-1)), look, 10);
            l.Pop(1);

            if (npc != null)
            {
                npc.Name = lookupName;
                npc.PacketName = packetName;
                npc.IsRenamed = true;
                npc.ReleaseTargIdOnDisappear = release;
                npc.Rotation = rotation;
                npc.X = x; npc.Y = y; npc.Z = z;
                npc.Moving = 0;
                npc.NameVis = (byte)FieldInt(l, table, "namevis", 0);
                npc.Status = NpcEntity.StatusNormal;
                npc.NamePrefix = 32;
                npc.Widescan = FieldInt(l, table, "widescan", 1) != 0;
                uint flags = (uint)FieldInt(l, table, "entityFlags", 0);
                if (flags != 0) npc.EntityFlags = flags;
                // Ensure that the npc is triggerable if onTrigger is passed in
                l.GetField(table, "onTrigger");
                if (l.IsFunction(-1)) npc.Triggerable = true;
                l.Pop(1);
                info.Npcs.Add(npc);
                info.NpcById[npc.Id] = npc;
                npc.UpdateMask |= EntityUpdatePacket.UpdateAllMob;
                return npc;
            }

            mob.Name = lookupName;
            mob.PacketName = packetName;
            mob.IsRenamed = true;
            mob.ReleaseTargIdOnDisappear = release;
            mob.Rotation = mob.SpawnRotation = rotation;
            mob.X = mob.SpawnX = x;
            mob.Y = mob.SpawnY = y;
            mob.Z = mob.SpawnZ = z;
            mob.Moving = 0;

            l.GetField(table, "mixins");
            if (l.IsTable(-1))
            {
                int mixins = l.Top;
                int top = l.Top;
                try
                {
                    // Use the global function "applyMixins"
                    l.GetGlobal("applyMixins");
                    if (l.IsFunction(-1))
                    {
                        l.PushObject(mob);
                        l.PushValue(mixins);
                        l.Call(2, 0);
                    }
                }
                catch (LuaException e)
                {
                    log_($"applyMixins: {mob.Name}: {e.Message}");
                }
                finally
                {
                    l.SetTop(top);
                }
            }
            l.Pop(1);

            // Allow for overrides of defaults on TYPE_MOB; if no value is specified, mob_groups.sql values are used
            byte minLevel = (byte)FieldInt(l, table, "minLevel", 0);
            if (minLevel > 0) mob.MinLevel = minLevel;
            else
            {
                log_($"luautils::GenerateDynamicEntity: No minLevel set for mob {mob.Name} in zone {info.Name}. Defaulting to 255.");
                mob.MinLevel = 255;
            }
            byte maxLevel = (byte)FieldInt(l, table, "maxLevel", 0);
            if (maxLevel > 0) mob.MaxLevel = maxLevel;
            else
            {
                log_($"luautils::GenerateDynamicEntity: No maxLevel set for mob {mob.Name} in zone {info.Name}. Defaulting to 255.");
                mob.MaxLevel = 255;
            }
            ushort dropId = (ushort)FieldInt(l, table, "dropId", 0);
            if (dropId > 0) mob.DropId = dropId;
            ushort skillList = (ushort)FieldInt(l, table, "skillList", 0);
            if (skillList > 0)
            {
                mob.MobSkillList = skillList;
                mob.SetMobMod(MobMod.SkillList, (short)skillList);
            }
            ushort spellList = (ushort)FieldInt(l, table, "spellList", 0);
            if (spellList > 0) SetMobSpellList(mob, spellList);
            uint respawn = (uint)FieldInt(l, table, "respawn", 0);
            if (respawn > 0)
            {
                if (mob.ReleaseTargIdOnDisappear)
                    log_($"luautils::GenerateDynamicEntity: {mob.Name} in zone {info.Name} asks for respawn but also releaseIdOnDisappear. It will not respawn.");
                mob.RespawnTimeSec = respawn;
                mob.AllowRespawn = true;
            }
            else mob.AllowRespawn = false;
            ushort spawnType = (ushort)FieldInt(l, table, "spawnType", 0);
            if (spawnType > 0) mob.SpawnType = spawnType;
            byte modelSize = (byte)FieldInt(l, table, "modelSize", 0);
            if (modelSize > 0) mob.ModelSize = Math.Min(modelSize, (byte)3);
            float modelHitboxSize = (float)FieldNumber(l, table, "modelHitboxSize", 0);
            if (modelHitboxSize > 0) mob.ModelHitboxSize = modelHitboxSize;

            info.Mobs.Add(mob);
            info.MobById[mob.Id] = mob;
            mob.CanSpawn = true;
            InitDynamicMob(info, mob);

            mob.IsAggroable = FieldBool(l, table, "isAggroable");
            mob.SpawnAnimation = (byte)(FieldBool(l, table, "specialSpawnAnimation") ? 1 : 0);
            uint mobFlags = (uint)FieldInt(l, table, "entityFlags", 0);
            if (mobFlags != 0) mob.EntityFlags = mobFlags;
            mob.UpdateMask |= EntityUpdatePacket.UpdateAllMob;
            return mob;
        }

        // the table's functions at xi.zones[zone][typeKey][lookupName] (created as needed); a mob always gets an
        // onMobDeath
        private void BindDynamicEntityFunctions(LuaState l, int table, string zoneName, string typeKey, string lookupName, bool isMob)
        {
            int top = l.Top;
            try
            {
                l.GetGlobal("xi");
                foreach (var key in new[] { "zones", zoneName, typeKey, lookupName })
                {
                    l.GetField(-1, key);
                    if (!l.IsTable(-1))
                    {
                        l.Pop(1);
                        l.NewTable();
                        l.PushValue(-1);
                        l.SetField(-3, key);
                    }
                    l.Remove(-2);
                }
                int entry = l.Top;
                bool hasDeath = false;
                l.PushNil();
                while (l.Next(table))
                {
                    if (l.IsFunction(-1) && l.IsString(-2))
                    {
                        string key = l.ToStringValue(-2);
                        if (key == "onMobDeath") hasDeath = true;
                        l.PushValue(-1);
                        l.SetField(entry, key);
                    }
                    l.Pop(1);
                }
                if (isMob && !hasDeath)
                {
                    l.DoString("return function() end", "=dynamic_entity", 1);
                    l.SetField(entry, "onMobDeath");
                }
            }
            finally
            {
                l.SetTop(top);
            }
        }

        // stringToLook: a 40-digit hex look (an optional 0x first), byte-swapped words; anything else is empty
        private static ushort[] StringToLook(string str)
        {
            var look = new ushort[10];
            if (str.Length == 42 && str[0] == '0' && str[1] == 'x') str = str.Substring(2);
            if (str.Length != 40) return look;
            var hex = new ushort[10];
            for (int i = 0; i < 10; i++)
            {
                ushort.TryParse(str.AsSpan(i * 4, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort value);
                hex[i] = (ushort)(value >> 8 | (value & 0xFF) << 8);
            }
            look[0] = hex[0];
            look[1] = hex[1];
            ushort[] masks = { 0x1000, 0x2000, 0x3000, 0x4000, 0x5000, 0x6000, 0x7000, 0x8000 };
            for (int i = 0; i < 8; i++) look[2 + i] = (ushort)(hex[2 + i] & ~masks[i]);
            return look;
        }

        private static string FieldString(LuaState l, int table, string key)
        {
            l.GetField(table, key);
            string v = l.IsString(-1) ? l.ToStringValue(-1) : null;
            l.Pop(1);
            return v;
        }

        // ---- the deletions -------------------------------------------------------------------------------------------

        // CZoneEntities::mobTick's end for a dynamic mob that disappeared: its links go, the chars forget it, it leaves
        // the zone and its battlefield, and its targid is freed later
        private void DeleteDynamicMob(ZoneInfo info, MobEntity m, long now)
        {
            if (m.PPet != null) m.PPet.PMaster = null;
            if (m.PMaster != null) m.PMaster.PPet = null;
            foreach (var other in info.Mobs) other.Enmity.Clear(m.Id);
            if (m.LinkParty != null)
            {
                m.LinkParty.Remove(m);
                m.LinkParty = null;
            }
            foreach (var (c, _) in charSessions_)
            {
                if (c.ClaimedMob == m) c.ClaimedMob = null;
                c.SpawnMobList.Remove(m.Id);
                if (c.EventPreparation.TargetEntity == m) c.EventPreparation.TargetEntity = null;
            }
            info.Mobs.Remove(m);
            info.MobById.Remove(m.Id);
            info.ReleaseDynamicTargId(m.TargId, now);
            // ~CBaseEntity
            if (m.PBattlefield != null) BattlefieldRemoveEntity(m.PBattlefield, m, Battlefield.LeaveWarpDc);
            lua_?.Forget(m);
        }

        // CZoneEntities::npcTick's end for a dynamic NPC that disappeared
        private void DeleteDynamicNpc(ZoneInfo info, NpcEntity npc, long now)
        {
            foreach (var (c, _) in charSessions_)
            {
                c.SpawnNpcList.Remove(npc.Id);
                if (c.EventPreparation.TargetEntity == npc) c.EventPreparation.TargetEntity = null;
            }
            info.Npcs.Remove(npc);
            info.NpcById.Remove(npc.Id);
            info.ReleaseDynamicTargId(npc.TargId, now);
            if (npc.PBattlefield != null) BattlefieldRemoveEntity(npc.PBattlefield, npc, Battlefield.LeaveWarpDc);
            lua_?.Forget(npc);
        }

        // ---- bindings ------------------------------------------------------------------------------------------------

        private void RegisterDynamicEntityBindings(Dictionary<string, LuaFunction> m)
        {
            // CLuaBaseEntity::instantiateMob: an ally from the zone's mob group, spawned where the char stands
            m["instantiateMob"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                var mob = InstantiateAlly(l.ToUIntArg(2), c.Zone);
                if (mob == null) return 0;
                mob.X = mob.SpawnX = c.X;
                mob.Y = mob.SpawnY = c.Y;
                mob.Z = mob.SpawnZ = c.Z;
                mob.Rotation = mob.SpawnRotation = c.Rotation;
                SpawnMob(AreaOf(c), mob);
                return 0;
            };
        }

        private void DynamicEntityZoneMethods(Dictionary<string, LuaFunction> zoneMethods)
        {
            // CLuaZone::insertDynamicEntity
            zoneMethods["insertDynamicEntity"] = l =>
            {
                var info = WeatherLuaZone(l);
                l.PushObject(l.IsTable(2) ? GenerateDynamicEntity(info, l, 2) : null);
                return 1;
            };
        }
    }
}
