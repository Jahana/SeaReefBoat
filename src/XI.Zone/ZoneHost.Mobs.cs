using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;

namespace XI.Zone
{
    // Mobs: zoneutils::InsertMobs and LoadMOBList build a zone's mobs from mobs.yaml, the zone's SpawnHandler spawns
    // and respawns them, and CZoneEntities::SpawnMOBs keeps each char's spawn list in step with what it can see.
    // The despawn sends the fade-out animation (0x038), a spawn resets the gil purse (MUG_GIL), and
    // main.NORMAL_MOB_MAX_LEVEL_RANGE_MIN / MAX cap normal mobs' levels at load. The AI is ZoneHost.MobAi.cs (roaming
    // ZoneHost.MobRoam.cs), the scripts and mixins ZoneHost.MobScripts.cs, the link parties ZoneHost.MobLink.cs, the
    // loot ZoneHost.Loot.cs.
    public sealed partial class ZoneHost
    {
        private Ecosystems ecosystems_;
        private MobStats mobStats_;
        public Random MobRng = new Random();

        // the zone's data as loaded, for the verify suites
        public ZoneInfo ZoneInfoOf(ushort id) => Zone(id).Info;

        private MobStats MobStatsFor() => mobStats_ ??= new MobStats(loader_.Grades, loader_.Stats.Growth) { AddTraits = TraitAddMobTraits, ResolveSpells = ResolveMobSpells, NavMeshOf = NavMeshOf, GilPurse = MobResetGilPurse };

        // CMobEntity::CanStealGil (CanDropGil: "TODO: Some mobs cannot be mugged") and ResetGilPurse: MUG_GIL is a
        // random gil drop cut by 4 to 6, or the whole drop when that comes to 0
        private void MobResetGilPurse(MobEntity m)
        {
            if (!CanDropGil(m)) return;
            uint purse = RandomGil(m) / (uint)MobRng.Next(4, 7);
            if (purse == 0) purse = RandomGil(m);
            m.SetMobMod(MobMod.MugGil, (short)purse);
        }

        // zoneutils::LoadMOBList for one zone: regions.yaml, then mobs.yaml's spawns, then the first spawn
        private void LoadMobs(ZoneInfo info)
        {
            if (info.Types.Contains("instanced") || loader_?.Stats == null) return;
            var dir = Path.Combine(dataDir_, "zones", ZoneInfo.FolderName(info.Name));
            var mobsPath = Path.Combine(dir, "mobs.yaml");
            if (!File.Exists(mobsPath)) return;
            ecosystems_ ??= Ecosystems.Load(dataDir_, enums_);
            var regionsPath = Path.Combine(dir, "regions.yaml");
            if (File.Exists(regionsPath)) info.Regions = RoamRegion.Load(regionsPath);
            info.Spawner = new MobSpawnHandler(this, info) { VanaHour = () => (int)VanaTime.GetHour(VanaTicksNow()), Rng = () => MobRng };
            InsertMobs(info, MobsFile.Load(mobsPath, enums_));
        }

        // the rest of LoadMOBList once the zone is in zones_, so a script that looks up a mob (GetMobByID) finds it:
        // the scripts and mixins, the saved modifiers, and the first spawns
        private void StartMobs(ZoneInfo info)
        {
            if (info.Spawner == null) return;
            InitMobScripts(info);
            long now = NowMs();
            foreach (var m in info.Mobs)
            {
                m.SaveModifiers();
                m.SaveMobModifiers();
                // a lottery, scripted or windowed spawn waits for its script
                m.AllowRespawn = !(m.SpawnType == SpawnType.Lottery || m.SpawnType == SpawnType.Scripted || m.SpawnType == SpawnType.Windowed);
                m.CanSpawn = m.SpawnWindow == null && (m.SpawnType == SpawnType.Normal || m.SpawnType == SpawnType.Lottery ||
                                                       m.SpawnType == SpawnType.Scripted || m.SpawnType == SpawnType.Windowed);
            }
            foreach (var m in info.Mobs)
            {
                // Skip mobs already registered via setRespawnTime in onMobInitialize - let SpawnHandler handle them
                if (info.Spawner.IsRegistered(m))
                {
                    if (m.SpawnType == SpawnType.Scripted && m.RespawnTimeSec > 0) m.AllowRespawn = true;
                    continue;
                }
                if (m.CanSpawn && m.AllowRespawn) TrySpawnMob(info, m);
                else
                {
                    // a scripted spawn with a respawn time from its initialize may respawn
                    if (m.SpawnType == SpawnType.Scripted && m.RespawnTimeSec > 0) m.AllowRespawn = true;
                    bool conditionBased = (m.SpawnType & (SpawnType.AtNight | SpawnType.AtEvening | SpawnType.Weather | SpawnType.Fog)) != 0 ||
                                          m.SpawnWindow != null;
                    info.Spawner.RegisterForRespawn(m, conditionBased ? 0 : null, now);
                }
            }
            int up = 0;
            foreach (var m in info.Mobs) if (m.IsSpawned) up++;
            log_($"zone: {info.Name} loaded {info.Mobs.Count} mobs, {up} spawned");
        }

        // InsertMobs; an instance run takes only the spawns its filter lets through
        private void InsertMobs(ZoneInfo info, MobsFile mobs, Func<MobSpawn, bool> filter = null)
        {
            bool dynamis = info.Types.Contains("dynamis");
            var slotByActIndex = new Dictionary<ushort, (uint SlotId, byte Chance, uint Cooldown)>();
            var slots = new Dictionary<uint, SpawnSlot>();
            // buildSpellList: a template that names its own inline spells gets one shared level-agnostic list
            // (0..255), resolved once per template before the spawns
            var spellListByTemplate = new Dictionary<string, MobSpellList>();
            foreach (var tpl in mobs.Templates.Values)
                if (tpl.Spells.Count > 0) spellListByTemplate[tpl.Name] = BuildSpellList(info.Id, tpl.Name, tpl.Spells);
            foreach (var slot in mobs.Slots)
            {
                // a lost roll falls back on the members without a chance, so a slot without one comes up empty
                if (!slot.Members.Exists(x => x.Chance == 0))
                    log_($"zone: InsertMobs: spawn slot {slot.Id} in zone {info.Id} gives every member a chance");
                foreach (var member in slot.Members) slotByActIndex[member.ActIndex] = (slot.Id, member.Chance, member.Cooldown);
            }

            foreach (var spawn in mobs.Spawns)
            {
                // a spawn with no template, or no position, only reserves its targid for script lookups
                if (spawn.TemplateName.Length == 0 || !spawn.Placed) continue;
                if (filter != null && !filter(spawn)) continue;
                var template = mobs.Templates[spawn.TemplateName];
                if (template.Content != null && !contentEnabled_(template.Content)) continue;
                if ((spawn.Id >> 12 & 0xFFF) != info.Id) throw new FormatException($"mob 0x{spawn.Id:X8} belongs to zone {spawn.Id >> 12 & 0xFFF}, not {info.Id}");
                if (!ecosystems_.SpeciesByName.TryGetValue(template.Species, out var species))
                    throw new FormatException($"mob template '{template.Name}' names unknown species '{template.Species}'");

                var m = new MobEntity
                {
                    Name = spawn.Script,
                    PacketName = template.DisplayName,
                    Id = spawn.Id,
                    TargId = spawn.ActIndex,
                    Zone = info.Id,
                    SpawnX = spawn.X, SpawnY = spawn.Y, SpawnZ = spawn.Z, SpawnRotation = spawn.Rotation,
                    X = spawn.X, Y = spawn.Y, Z = spawn.Z, Rotation = spawn.Rotation,
                    MinLevel = spawn.MinLevel,
                    MaxLevel = spawn.MaxLevel,
                    Type = template.Type,
                    Species = species.Id,
                };
                AttachEffects(m);

                // ecosystem -> family -> species -> template -> spawn
                var a = species.Attributes.Clone();
                a.Merge(template.Attributes);
                a.Merge(spawn.Attributes);
                MobStats.ApplySpecies(m, species, a);

                m.WeaponSkill = a.CombatSkill ?? 0;
                m.MainDelay = m.BaseDelay = a.Delay ?? 240;
                m.DamageMultiplier = a.DamageMultiplier ?? 100;
                if (a.Look != null) m.Look = (ushort[])a.Look.Clone();
                m.HpModifier = a.Stats.Hp ?? 0;
                m.MpModifier = a.Stats.Mp ?? 0;
                m.RespawnTimeSec = a.Respawn ?? 0;
                m.SpawnType = a.SpawnType ?? SpawnType.Normal;
                m.SpawnWindow = a.SpawnWindow;
                m.NamePrefix = a.NamePrefix ?? 0;
                m.Moving = a.Moving ?? 0;

                // main.NORMAL_MOB_MAX_LEVEL_RANGE_MIN/MAX let a server flatten normal mob levels; notorious mobs keep theirs.
                if (loader_.Stats?.Settings is { } levelSettings && !m.IsNotorious)
                {
                    if (levelSettings.NormalMobMaxLevelRangeMin > 0 && m.MinLevel > levelSettings.NormalMobMaxLevelRangeMin)
                        m.MinLevel = (byte)levelSettings.NormalMobMaxLevelRangeMin;
                    if (levelSettings.NormalMobMaxLevelRangeMax > 0 && m.MaxLevel > levelSettings.NormalMobMaxLevelRangeMax)
                        m.MaxLevel = (byte)levelSettings.NormalMobMaxLevelRangeMax;
                }
                m.EntityFlags = a.EntityFlags ?? 0;
                m.Animation = a.Animation ?? 0;
                m.AnimationSub = a.AnimationSub ?? 0;
                if (m.AnimationSub != 0) m.SetMobMod(MobMod.SpawnAnimationsub, m.AnimationSub);
                m.Pool = template.Id;
                m.Template = template;
                m.TemplateLoot = !template.Loot.Empty;
                m.Allegiance = template.Allegiance;
                m.NameVis = a.NameVis ?? 0;
                m.ModelHitboxSize = Math.Max(0.0f, (a.Hitbox ?? 0) / 10.0f);
                m.ModelSize = a.ModelSize ?? 0;
                m.RoamFlags = template.RoamFlags;
                m.MobSkillList = template.SkillList;
                m.SpellListId = template.SpellList;
                // m_SpellListContainer: the template's own inline spell list (buildSpellList) when it named spells, else
                // the registry list for spell_list_id (id 0 is the empty list, so a spell-less mob's container is non-null)
                m.SpellList = spellListByTemplate.TryGetValue(template.Name, out var inlineList)
                    ? inlineList : GetMobSpellList(template.SpellList);
                foreach (var name in spawn.Regions)
                {
                    if (!info.Regions.TryGetValue(name, out var region))
                        throw new FormatException($"InsertMobs: spawn {spawn.Id} names region '{name}', which the zone does not declare");
                    m.Regions.Add(region);
                }
                // setRoamRegions: a region has no leeway, its edge is the limit (SetupRoaming / SetupEventMob may
                // still set their own later)
                if (spawn.Regions.Count > 0) m.MaxRoamDistance = 0.0f;
                // setPatrolRoute: the loop is entered at a random waypoint, which becomes the spawn point
                m.Route = spawn.Route;
                if (spawn.Route.Count > 0)
                {
                    int start = MobRng.Next(spawn.Route.Count);
                    m.Route = new List<(float X, float Y, float Z)>(spawn.Route.Count);
                    for (int k = 0; k < spawn.Route.Count; k++) m.Route.Add(spawn.Route[(start + k) % spawn.Route.Count]);
                    (m.SpawnX, m.SpawnY, m.SpawnZ) = m.Route[0];
                    m.SpawnRotation = 0;
                }

                if (slotByActIndex.TryGetValue(spawn.ActIndex, out var placement))
                {
                    if (!slots.TryGetValue(placement.SlotId, out var slot)) slots[placement.SlotId] = slot = new SpawnSlot(placement.SlotId);
                    if (m.SpawnType == SpawnType.Scripted)
                        log_($"zone: Mob with ID {m.Id} in spawn slot {placement.SlotId} in zone {info.Id} is a scripted spawn. Scripted spawns should not be assigned to spawn slots.");
                    slot.AddMob(m, placement.Chance, placement.Cooldown * 1000L);
                }
                if (dynamis) m.SetMobMod(MobMod.Charmable, 0);

                MobStats.InitializeMob(m);
                foreach (var (mod, value) in a.Mods) m.AddModifier(mod, value);
                foreach (var (mod, value) in a.MobMods) m.SetMobMod(mod, value);

                info.Mobs.Add(m);
                info.MobById[m.Id] = m;
            }
        }

        // CMobEntity::TrySpawn: a slot member spawns through its slot
        private bool TrySpawnMob(ZoneInfo info, MobEntity m)
        {
            if (!m.AllowRespawn || m.IsSpawned) return false;
            if (m.Slot != null)
            {
                info.Spawner.TrySpawnSlot(m.Slot, null, NowMs());
                return false;
            }
            if (!m.CanSpawn) return false;
            SpawnMob(info, m);
            return true;
        }

        internal void SpawnMob(ZoneInfo info, MobEntity m)
        {
            // CBaseEntity::Spawn: the local vars, the previous life's data, and PAI->Reset's action and timer queues
            m.LocalVars.Clear();
            ResetEntityData(m);
            // CBattleEntity::Spawn
            m.BattleId = 0;
            // CMobEntity::Spawn: the stolen item comes back in a battlefield, or after the mob died
            if ((m.Type & MobType.Battlefield) != 0 || m.Hp == 0) m.ItemStolen = m.ItemDespoiled = false;
            ClearActionQueues(m);
            // PAI->Reset retires the current state, so a CDespawnState under way (a battlefield's cleanup just
            // before the arena is entered again) ends without its fade-out
            m.FadeAtMs = 0;
            m.PathFind?.Clear();
            ResetCombat(m);
            MobControllerReset(m, NowMs());
            MobStatsFor().Spawn(m, MobRng, info.Types.Contains("instanced"), NowMs(), OnMobSpawn);
            MobRoamOnSpawn(m, NowMs());
        }

        // CMobEntity::SetDespawnTime ran out: CDespawnState::init (the "kesu" fade-out animation, 0x038, unless gone
        // already or NoDespawn) registers the respawn, and 3 s later the mob fades out
        private void DespawnMob(ZoneInfo info, MobEntity m, long now)
        {
            m.DespawnAtMs = 0;
            AbortMobSkill(m, now);
            AbortMagic(m, now);
            const ushort behaviorNoDespawn = 0x001;
            if (m.Status != NpcEntity.StatusDisappear && (m.Behavior & behaviorNoDespawn) == 0) PushCharsNear(m, Schedulor(m, m, "kesu"));
            if (m.AllowRespawn) info.Spawner?.RegisterForRespawn(m, null, now);
            m.FadeAtMs = now + 3000;
        }

        // the zone's mobs each tick: a pending despawn, the fade-out, and the updates CMobEntity::PostTick sends at
        // most every 250 ms to the chars that have the mob spawned; then the spawn handler, every 30 s. A char's spawn
        // list follows on its next position update (0x015), as in the C++.
        private void TickMobs()
        {
            long now = NowMs();
            // a script may load another zone (GetMobByID) while this runs
            foreach (var (info, _, _) in AllAreas())
            {
                // a zone with no mobs has no spawn handler; an instance run has its mobs and none (they do not respawn)
                if (info.Spawner == null && info.Mobs.Count == 0) continue;
                List<MobEntity> released = null;
                // by index: a script may add a dynamic mob while this runs
                for (int i = 0; i < info.Mobs.Count; i++)
                {
                    var m = info.Mobs[i];
                    // This is only valid for dynamic entities
                    if (m.Status == NpcEntity.StatusDisappear && m.ReleaseTargIdOnDisappear)
                    {
                        (released ??= new List<MobEntity>()).Add(m);
                        continue;
                    }
                    if (!m.IsSpawned) continue;
                    // CZoneEntities::ZoneServer (mob): expired spell and ability recasts go
                    m.Recasts?.Check();
                    const ushort behaviorNoDespawn = 0x001;
                    if (m.FadeAtMs == 0 && m.DespawnAtMs > 0 && m.DespawnAtMs < now) DespawnMob(info, m, now);
                    if (m.FadeAtMs != 0 && now >= m.FadeAtMs && (m.Behavior & behaviorNoDespawn) == 0)
                    {
                        // CMobEntity::OnDespawn: an idle pet leaves with its master, a fighting one once its fight ends
                        m.FadeAtMs = 0;
                        m.FadeOut();
                        if (m.PPet is MobEntity pet && pet.PMaster == m && pet.GetMobMod(MobMod.NoDespawn) == 0) pet.DespawnAtMs = now + 1;
                        OnMobDespawn(m);
                        TriggerListener(m, "DESPAWN", m);
                    }
                    if (m.UpdateMask != 0 && now > m.NextUpdateMs)
                    {
                        m.NextUpdateMs = now + 250;
                        foreach (var (c, s) in charSessions_)
                            if (InArea(c, info) && c.SpawnMobList.Contains(m.Id)) UpdateEntityPacket(s, m, EntityUpdate.Update, m.UpdateMask);
                        // If this mob is charmed, it should sync with its master
                        if (m.PMaster is CharData master && master.PPet == m) PushTo(master, PetSyncPacket.Create(master));
                        m.UpdateMask = 0;
                    }
                }
                if (released != null)
                    foreach (var m in released) DeleteDynamicMob(info, m, now);
                info.EraseStaleDynamicTargIds(now);
                info.Spawner?.Tick(now);
            }
        }

        // SpawnHandler::onGameHour: a mob up outside its spawn window despawns
        private void MobsOnGameHour(uint hour)
        {
            long now = NowMs();
            foreach (var (info, _, _) in zones_.Values)
            {
                if (info.Spawner == null) continue;
                foreach (var m in info.Mobs)
                {
                    if (MobSpawnHandler.SpawnWindowOf(m) is { } window && m.IsSpawned && m.IsAlive &&
                        !MobSpawnHandler.HourInWindow(hour, window.Start, window.End))
                        m.DespawnAtMs = now + 1;
                }
            }
        }

        // ---- mob spawns --------------------------------------------------------------------------------------------
        // CZoneEntities::SpawnMOBs and syncSpawnListWithGrid: a mob is visible when it is up, within 50 yalms and
        // no more than 20 above or below the char's eyes; each one visible, new or kept, gets its aggro check

        private static bool Visible(CharData c, MobEntity m)
        {
            if (m.Status == NpcEntity.StatusDisappear) return false;
            if (Math.Abs(m.Y - c.Y - 0.5f) > 20.0f) return false;
            float dx = c.X - m.X, dy = c.Y - m.Y, dz = c.Z - m.Z;
            return dx * dx + dy * dy + dz * dz <= EntityRenderDistance * EntityRenderDistance;
        }

        private void SpawnMobs(MapSession s, CharData c)
        {
            var zone = AreaOf(c);
            var gone = new List<uint>();
            foreach (var id in c.SpawnMobList)
                if (!zone.MobById.TryGetValue(id, out var m) || !Visible(c, m)) gone.Add(id);
            foreach (var id in gone)
            {
                c.SpawnMobList.Remove(id);
                if (zone.MobById.TryGetValue(id, out var m)) UpdateEntityPacket(s, m, EntityUpdate.Despawn, 0);
            }
            foreach (var m in zone.Mobs)
            {
                if (!Visible(c, m)) continue;
                if (c.SpawnMobList.Add(m.Id)) UpdateEntityPacket(s, m, EntityUpdate.Spawn, EntityUpdatePacket.UpdateAllMob);
                TapMobAggro(c, m);
            }
        }

        private static void UpdateEntityPacket(MapSession s, MobEntity m, EntityUpdate type, byte updatemask)
        {
            foreach (var pending in s.PacketList)
            {
                if (pending.GetOpcode() == 0x00E && BitConverter.ToUInt32(pending.Buffer, 4) == m.Id)
                {
                    EntityUpdatePacket.UpdateWith(pending, m, type, updatemask);
                    return;
                }
            }
            s.PacketList.AddLast(EntityUpdatePacket.Mob(m, type, updatemask));
        }
    }

    // SpawnHandler: the mobs waiting to respawn, alone or as a spawn slot. Tick spawns what is due within half its
    // 30 s interval, whose conditions hold and whose script's onMobSpawnCheck lets it.
    public sealed class MobSpawnHandler
    {
        public const long IntervalMs = 30000, WindowMs = IntervalMs / 2;

        private readonly ZoneHost host_;
        private readonly ZoneInfo zone_;
        private readonly Dictionary<uint, long> pendingRespawns_ = new Dictionary<uint, long>();
        private readonly Dictionary<SpawnSlot, (long RespawnAt, uint? SpecificMobId)> pendingSlotRespawns_ =
            new Dictionary<SpawnSlot, (long, uint?)>();
        private long nextTickMs_;

        public Func<int> VanaHour = () => 0;
        public Func<Random> Rng = () => Random.Shared;

        public MobSpawnHandler(ZoneHost host, ZoneInfo zone)
        {
            host_ = host;
            zone_ = zone;
        }

        public static bool HourInWindow(uint hour, byte spawn, byte despawn) =>
            spawn <= despawn ? hour >= spawn && hour < despawn : hour >= spawn || hour < despawn;

        public static (byte Start, byte End)? SpawnWindowOf(MobEntity m)
        {
            if (m.SpawnWindow != null) return m.SpawnWindow;
            if ((m.SpawnType & SpawnType.AtNight) != 0) return (20, 4);
            if ((m.SpawnType & SpawnType.AtEvening) != 0) return (18, 6);
            return null;
        }

        // registerForRespawn: respawnTimeSec null is the mob's own respawn time
        public void RegisterForRespawn(MobEntity m, uint? respawnTimeSec, long nowMs)
        {
            if (m == null || !m.AllowRespawn) return;
            long respawnAt = nowMs + (respawnTimeSec ?? m.RespawnTimeSec) * 1000L;
            if (m.Slot != null)
                pendingSlotRespawns_[m.Slot] = (respawnAt, respawnTimeSec is uint t && t > 0 ? m.Id : null);
            else pendingRespawns_[m.Id] = respawnAt;
        }

        // unregister: the slot's pending respawn when the mob has one, else the mob's
        public void Unregister(MobEntity m)
        {
            if (m.Slot != null) pendingSlotRespawns_.Remove(m.Slot);
            else pendingRespawns_.Remove(m.Id);
        }

        public bool IsRegistered(MobEntity m) => m.Slot != null ? pendingSlotRespawns_.ContainsKey(m.Slot) : pendingRespawns_.ContainsKey(m.Id);

        public long? RemainingRespawnMs(MobEntity m, long nowMs)
        {
            if (m.Slot != null)
                return pendingSlotRespawns_.TryGetValue(m.Slot, out var p) ? Math.Max(p.RespawnAt - nowMs, 0) : null;
            return pendingRespawns_.TryGetValue(m.Id, out var at) ? Math.Max(at - nowMs, 0) : null;
        }

        // the zone timer fires every 30 s; Tick runs from the zone tick, so it waits out the interval itself
        public void Tick(long nowMs)
        {
            if (nowMs < nextTickMs_) return;
            nextTickMs_ = nowMs + IntervalMs;
            long threshold = nowMs + WindowMs;
            var toSpawn = new List<MobEntity>();
            foreach (var (id, at) in new List<KeyValuePair<uint, long>>(pendingRespawns_))
            {
                if (at > threshold) continue;
                if (!zone_.MobById.TryGetValue(id, out var m))
                {
                    pendingRespawns_.Remove(id);
                    continue;
                }
                if (!CanSpawnNow(m) || host_.OnMobSpawnCheck(m) != 0) continue;
                toSpawn.Add(m);
                pendingRespawns_.Remove(id);
            }
            foreach (var m in toSpawn) host_.SpawnMob(zone_, m);
            foreach (var (slot, p) in new List<KeyValuePair<SpawnSlot, (long RespawnAt, uint? SpecificMobId)>>(pendingSlotRespawns_))
            {
                if (p.RespawnAt > threshold) continue;
                if (TrySpawnSlot(slot, p.SpecificMobId, nowMs)) pendingSlotRespawns_.Remove(slot);
            }
        }

        public bool TrySpawnSlot(SpawnSlot slot, uint? specificMobId, long nowMs) =>
            slot.TrySpawn(specificMobId, CanSpawnNow, host_.OnMobSpawnCheck, m => host_.SpawnMob(zone_, m), nowMs, Rng());

        // canSpawnNow: the time of day, then the weather (fog, and an elemental's element)
        public bool CanSpawnNow(MobEntity m)
        {
            if (m == null || !m.AllowRespawn) return false;
            if (SpawnWindowOf(m) is { } window && !HourInWindow((uint)VanaHour(), window.Start, window.End)) return false;
            const ushort weatherFog = 3;
            if ((m.SpawnType & SpawnType.Fog) != 0 && zone_.Weather != weatherFog) return false;
            if ((m.SpawnType & SpawnType.Weather) != 0 && m.Ecosystem == Ecosystem.Elemental && m.Element != WeatherElement(zone_.Weather))
                return false;
            return true;
        }

        // zoneutils::GetWeatherElement
        public static byte WeatherElement(ushort weather)
        {
            byte[] element = { 0, 0, 0, 0, 1, 1, 6, 6, 4, 4, 3, 3, 2, 2, 5, 5, 7, 7, 8, 8 };
            return weather < element.Length ? element[weather] : (byte)0;
        }
    }
}
