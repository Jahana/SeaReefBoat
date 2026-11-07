using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // INSTANCE_STATUS
    public enum InstanceStatus : byte { Normal, Failed, Complete }

    // instanceutils::InstanceData_t: an instance_list row with its zone's name and the script it runs
    public sealed class InstanceData
    {
        public ushort Id;
        public string Name = "";
        public ushort Zone, EntranceZone;
        public uint TimeLimitMin;
        public float StartX, StartY, StartZ;
        public byte StartRot;
        public ushort? MusicDay, MusicNight, BattleSolo, BattleMulti;
        public uint OverlayId;
        public string ZoneName = "";
        public string Filename = "";
    }

    // CInstance: one run of an instance in an instanced zone, with its own entities (CZoneEntities)
    public sealed class Instance
    {
        public uint RunId;
        public InstanceData Data;
        public ZoneInfo Info; // the run's entities: an instanced zone's own lists stay empty
        public ushort Id => Data.Id;
        public ushort Zone => Data.Zone;
        public uint Commander;
        public byte LevelCap;
        public long TimeLimitMs, StartMs, LastTimeUpdateMs, LastTimeCheckMs, WipeAtMs;
        public uint Progress, Stage;
        public float EntryX, EntryY, EntryZ;
        public byte EntryRot;
        public InstanceStatus Status;
        public readonly List<uint> RegisteredChars = new List<uint>();
        public readonly HashSet<uint> EnteredChars = new HashSet<uint>();
        public readonly Dictionary<string, ulong> LocalVars = new Dictionary<string, ulong>();
        public readonly List<MobEntity> Allies = new List<MobEntity>();

        public bool Failed => Status == InstanceStatus.Failed;
        public bool Completed => Status == InstanceStatus.Complete;
    }

    // CZoneInstance, CInstance, CInstanceLoader, instanceutils, the luautils instance hooks and CLuaInstance. Each run has
    // its own ZoneInfo, loaded from the instanced zone's mobs.yaml / npcs.yaml for the ids instance_entities lists (the
    // C++ reads the same spawns from mob_spawn_points / npc_list); an entity or char in a run looks things up through it
    // (AreaOf), and chars only meet the entities of their own run (SameArea); the zone's trigger areas, kept by zone id,
    // serve its runs' chars as CZoneInstance::CheckTriggerAreas does. instanceutils serves the instances whose zone this
    // map process is assigned (ProcessIpp; 0 serves them all), loaded at startup or, with LazyZones, on first use.
    public sealed partial class ZoneHost
    {
        private const string InstanceLuaType = "CInstance";
        private const ushort ZoneNoDestination = 0xFFFF; // ZONE_NO_DESTINATION
        private const byte TypePc = 0x01, TypeNpc = 0x02, TypeMob = 0x04, TypePet = 0x08, TypeShip = 0x10, TypeTrust = 0x20;

        // CZoneInstance::m_InstanceList per instanced zone
        private readonly Dictionary<ushort, List<Instance>> instances_ = new Dictionary<ushort, List<Instance>>();
        // instanceutils::InstanceData (each row with its script) and lazyLoad's managed ids
        private Dictionary<ushort, InstanceData> instanceData_;
        private HashSet<ushort> managedInstances_;
        private bool instancesLazy_;

        // MapConfig::ipp: the address this map process serves (zone_settings zoneip / zoneport), 0 for every zone
        public ulong ProcessIpp;
        // MapConfig::lazyZones (--lazy): load on first access rather than at startup
        public bool LazyZones;
        // instanceutils::LoadQueue: (requester, instance id), one served at the end of each tick
        private readonly Queue<(uint CharId, ushort InstanceId)> instanceLoadQueue_ = new Queue<(uint, ushort)>();
        private uint nextInstanceRunId_ = 1;
        private readonly Dictionary<ushort, MobsFile> instanceMobsFiles_ = new Dictionary<ushort, MobsFile>();

        // ---- the routing ----------------------------------------------------------------------------------------------

        // an entity's instance counts only inside that instance's zone: a non-instanced CZone ignores PInstance
        private static Instance EffectiveInstance(Instance i, ushort zone) => i != null && i.Zone == zone ? i : null;

        // the area an entity's lookups go through: its instance's, else its zone's
        private ZoneInfo AreaOf(BattleEntity e) => EffectiveInstance(e.Instance, e.Zone)?.Info ?? Zone(e.Zone).Info;
        private ZoneInfo AreaOf(NpcEntity n) => EffectiveInstance(n.Instance, ZoneOf(n))?.Info ?? Zone(ZoneOf(n)).Info;
        private ZoneInfo AreaOf(object e) => e is BattleEntity b ? AreaOf(b) : AreaOf((NpcEntity)e);

        private static Instance InstanceOf(object e) =>
            e is BattleEntity b ? EffectiveInstance(b.Instance, b.Zone) : EffectiveInstance(((NpcEntity)e).Instance, ZoneOf(e));

        // the same zone and the same instance (or neither in one)
        internal static bool SameArea(object a, object b) => ZoneOf(a) == ZoneOf(b) && InstanceOf(a) == InstanceOf(b);

        // whether the entity's lookups go through this area; a zone not loaded here is not the area, so nothing loads (a
        // char this process put out to another process's zone is still in its sessions until it leaves)
        private bool InArea(object e, ZoneInfo info)
        {
            var instance = InstanceOf(e);
            if (instance != null) return instance.Info == info;
            return zones_.TryGetValue(ZoneOf(e), out var z) && z.Info == info;
        }

        // every zone loaded and every instance running in one, for the per-area ticks (a snapshot: a tick may load a zone)
        private List<(ZoneInfo Info, uint Ip, ushort Port)> AllAreas()
        {
            var list = new List<(ZoneInfo, uint, ushort)>(zones_.Values);
            foreach (var (zone, runs) in instances_)
                if (zones_.TryGetValue(zone, out var z))
                    foreach (var run in runs) list.Add((run.Info, z.Ip, z.Port));
            return list;
        }

        private static bool IsInstancedZone(ZoneInfo info) => info.Types.Contains("instanced");

        // ---- instanceutils --------------------------------------------------------------------------------------------

        // instanceutils::Initialize: the instances assigned to this process, loaded now or (lazyZones) on first access.
        // The host calls it at startup; the first lookup does if it has not.
        public void InitializeInstances()
        {
            if (instanceData_ != null) return;
            instanceData_ = new Dictionary<ushort, InstanceData>();
            var ids = InstancesAssignedToThisProcess();
            if (!LazyZones)
            {
                LoadInstances(ids);
                return;
            }
            instancesLazy_ = true;
            managedInstances_ = new HashSet<ushort>(ids);
        }

        // instanceutils::GetInstancesAssignedToThisProcess: the instances whose zone has this process's address, or all
        private List<ushort> InstancesAssignedToThisProcess()
        {
            uint ip = XI.Map.MapIpp.Ip(ProcessIpp);
            string ipString = XI.Map.MapIpp.ToString(ProcessIpp).Split(':')[0];
            var r = Exec("SELECT instanceid FROM instance_list INNER JOIN zone_settings ON instance_zone = zone_settings.zoneid " +
                         "WHERE IF(? <> 0, ? = zoneip AND ? = zoneport, TRUE)",
                BoundValue.Of(ip), BoundValue.Of(ipString), BoundValue.Of(XI.Map.MapIpp.Port(ProcessIpp)));
            var ids = new List<ushort>();
            while (r != null && r.Next()) ids.Add(r.Get<ushort>("instanceid"));
            return ids;
        }

        // instanceutils::LoadInstances: the rows asked for, each script cached (an assault's from scripts/assaults,
        // overwriting, else the zone's instances folder)
        private void LoadInstances(List<ushort> ids)
        {
            if (ids.Count == 0) return;
            var r = Exec("SELECT instanceid, instance_name, instance_zone, entrance_zone, time_limit, start_x, start_y, start_z, start_rot, " +
                         "instance_list.music_day, instance_list.music_night, instance_list.battlesolo, instance_list.battlemulti, overlay_id, " +
                         "zone_settings.name AS zone_name FROM instance_list INNER JOIN zone_settings ON instance_zone = zone_settings.zoneid " +
                         "WHERE FIND_IN_SET(instanceid, ?)", BoundValue.Of(string.Join(",", ids)));
            var loaded = new List<InstanceData>();
            while (r != null && r.Next())
            {
                var d = new InstanceData
                {
                    Id = r.Get<ushort>("instanceid"),
                    Name = r.Get<string>("instance_name") ?? "",
                    Zone = r.Get<ushort>("instance_zone"),
                    EntranceZone = r.Get<ushort>("entrance_zone"),
                    TimeLimitMin = r.Get<uint>("time_limit"),
                    StartX = r.Get<float>("start_x"),
                    StartY = r.Get<float>("start_y"),
                    StartZ = r.Get<float>("start_z"),
                    StartRot = (byte)r.Get<uint>("start_rot"),
                    MusicDay = r.IsNull("music_day") ? null : r.Get<ushort>("music_day"),
                    MusicNight = r.IsNull("music_night") ? null : r.Get<ushort>("music_night"),
                    BattleSolo = r.IsNull("battlesolo") ? null : r.Get<ushort>("battlesolo"),
                    BattleMulti = r.IsNull("battlemulti") ? null : r.Get<ushort>("battlemulti"),
                    OverlayId = r.IsNull("overlay_id") ? 0 : r.Get<uint>("overlay_id"),
                    ZoneName = r.Get<string>("zone_name") ?? "",
                };
                instanceData_[d.Id] = d;
                loaded.Add(d);
            }
            foreach (var d in loaded)
            {
                // Determine if instance exists at new assault path
                d.Filename = $"./scripts/assaults/{d.ZoneName}/{d.Name}.lua";
                if (File.Exists(d.Filename)) scripts_?.LoadLuaObjectFromFile(d.Filename, true);
                else
                {
                    // If not, fall back to regular instance path
                    d.Filename = $"./scripts/zones/{d.ZoneName}/instances/{d.Name}.lua";
                    scripts_?.LoadLuaObjectFromFile(d.Filename);
                }
            }
        }

        // instanceutils::GetInstanceData: a managed instance loads on its first lookup under lazy zones; an empty one for
        // an unknown id
        private InstanceData InstanceDataOf(ushort id)
        {
            InitializeInstances();
            if (instanceData_.TryGetValue(id, out var d)) return d;
            if (instancesLazy_ && managedInstances_.Contains(id))
            {
                LoadInstances(new List<ushort> { id });
                if (instanceData_.TryGetValue(id, out d)) return d;
                log_($"instanceutils::GetInstanceData: managed instanceid {id} has no row in instance_list");
            }
            return new InstanceData();
        }

        // instanceutils::IsValidInstanceID: loaded, or managed and yet to load
        private bool IsValidInstanceId(ushort id)
        {
            InitializeInstances();
            return instanceData_.ContainsKey(id) || instancesLazy_ && managedInstances_.Contains(id);
        }

        // the instances this process has loaded so far (and, lazily, those it would load)
        public int LoadedInstanceCount { get { InitializeInstances(); return instanceData_.Count; } }
        public int ManagedInstanceCount { get { InitializeInstances(); return instancesLazy_ ? managedInstances_.Count : instanceData_.Count; } }

        // instanceutils::CheckInstance at the end of the tick: the oldest request loads, once its requester is still here
        private void CheckInstanceLoadQueue()
        {
            if (instanceLoadQueue_.Count == 0) return;
            var (charId, instanceId) = instanceLoadQueue_.Dequeue();
            var (requester, _) = CharById(charId);
            if (requester == null)
            {
                log_("Encountered invalid requester id when loading instance!");
                return;
            }
            // CInstanceLoader requires the instance template zone to be loaded.
            IsZoneReady(InstanceDataOf(instanceId).Zone);
            LoadInstance(instanceId, requester);
        }

        // ---- CInstanceLoader ------------------------------------------------------------------------------------------

        // CInstanceLoader: a new run in the instanced zone (CZoneInstance::CreateInstance and CInstance::LoadInstance),
        // its mobs and NPCs, their scripts, then onInstanceCreatedCallback for the requester and onInstanceCreated
        private Instance LoadInstance(ushort instanceId, CharData requester)
        {
            var data = InstanceDataOf(instanceId);
            // zoneutils::GetZone: the instanced zone on this process
            var zoneInfo = data.Zone == 0 && data.Name.Length == 0 ? null : HeldZone(data.Zone);
            if (zoneInfo == null || !IsInstancedZone(zoneInfo))
            {
                log_($"Invalid zone for instanceid: {instanceId}");
                return null;
            }

            long now = NowMs();
            var run = new Instance
            {
                RunId = nextInstanceRunId_++,
                Data = data,
                TimeLimitMs = data.TimeLimitMin * 60_000L,
                StartMs = now,
                LastTimeCheckMs = now,
                WipeAtMs = now,
                EntryX = data.StartX, EntryY = data.StartY, EntryZ = data.StartZ, EntryRot = data.StartRot,
            };
            run.Info = ZoneInfo.Load(Path.Combine(dataDir_, "zones"), zoneInfo.Id, zoneInfo.Name, ZoneIdByName);
            run.Info.Regions = zoneInfo.Regions;
            // CInstance::GetBackgroundMusicDay and the rest: the run's own music, else the zone's
            run.Info.MusicDay = data.MusicDay ?? zoneInfo.MusicDay;
            run.Info.MusicNight = data.MusicNight ?? zoneInfo.MusicNight;
            run.Info.BattleSolo = data.BattleSolo ?? zoneInfo.BattleSolo;
            run.Info.BattleParty = data.BattleMulti ?? zoneInfo.BattleParty;
            if (!instances_.TryGetValue(data.Zone, out var runs)) instances_[data.Zone] = runs = new List<Instance>();
            runs.Add(run);
            if (data.Name.Length > 0 && scripts_ != null) scripts_.LoadLuaObjectFromFile(data.Filename, data.Filename.Contains("/assaults/"));

            // the entities instance_entities names, in the overlay zone's id range when the run has one
            var ids = new HashSet<uint>();
            var r = Exec("SELECT id FROM instance_entities WHERE instanceid = ?", BoundValue.Of(instanceId));
            while (r != null && r.Next()) ids.Add(r.Get<uint>("id"));
            uint effectiveZone = data.OverlayId != 0 ? data.OverlayId : zoneInfo.Id;
            InsertInstanceMobs(run, ids, effectiveZone);
            InsertInstanceNpcs(run, ids, effectiveZone);

            if (!run.Failed)
            {
                // Cache every entity script before running any handler; then the mobs' initialize, parties and mixins
                InitMobScripts(run.Info);
                foreach (var m in run.Info.Mobs)
                {
                    m.SaveModifiers();
                    m.SaveMobModifiers();
                }
                StartNpcs(run.Info);
                OnInstanceCreatedCallback(requester, run);
                OnInstanceCreated(run);
            }
            return run;
        }

        // the zone's mobs.yaml spawns the run lists (a spawn with no position is left out, as NOT (pos = 0))
        private void InsertInstanceMobs(Instance run, HashSet<uint> ids, uint effectiveZone)
        {
            if (loader_?.Stats == null) return;
            var info = run.Info;
            if (!instanceMobsFiles_.TryGetValue(info.Id, out var mobs))
            {
                var path = Path.Combine(dataDir_, "zones", ZoneInfo.FolderName(info.Name), "mobs.yaml");
                ecosystems_ ??= Ecosystems.Load(dataDir_, enums_);
                mobs = File.Exists(path) ? MobsFile.Load(path, enums_) : null;
                instanceMobsFiles_[info.Id] = mobs;
            }
            if (mobs == null) return;
            InsertMobs(info, mobs, spawn => ids.Contains(spawn.Id) && (spawn.Id >> 12 & 0xFFF) == effectiveZone);
            foreach (var m in info.Mobs)
            {
                m.Instance = run;
                // If a special instanced mob aggros, it should always aggro regardless of level.
                if ((m.Type & MobType.Event) != MobType.Normal && m.Aggro) m.SetMobMod(MobMod.AlwaysAggro, 1);
            }
        }

        // the zone's npcs.yaml entries the run lists, in the zone's NPC id range
        private void InsertInstanceNpcs(Instance run, HashSet<uint> ids, uint effectiveZone)
        {
            var info = run.Info;
            var path = Path.Combine(dataDir_, "zones", ZoneInfo.FolderName(info.Name), "npcs.yaml");
            if (!File.Exists(path)) return;
            uint zoneMin = (effectiveZone << 12) + 0x1000000, zoneMax = zoneMin + 1024;
            foreach (var npc in NpcEntity.Load(path, enums_, contentEnabled_))
            {
                if (!ids.Contains(npc.Id) || npc.Id < zoneMin || npc.Id >= zoneMax) continue;
                npc.Instance = run;
                info.Npcs.Add(npc);
                info.NpcById[npc.Id] = npc;
            }
        }

        // ---- CInstance ------------------------------------------------------------------------------------------------

        // the chars in the run: CZoneEntities::m_charList
        private List<CharData> InstanceChars(Instance run)
        {
            var list = new List<CharData>();
            foreach (var (c, s) in charSessions_)
                if (s.InZone && s.Char == c && InstanceOf(c) == run) list.Add(c);
            return list;
        }

        // CInstance::RegisterChar: the first one registered is the commander
        private static void RegisterInstanceChar(Instance run, CharData c)
        {
            if (run.RegisteredChars.Count == 0) run.Commander = c.Id;
            run.RegisteredChars.Add(c.Id);
        }

        // CInstance::SetProgress
        private void SetInstanceProgress(Instance run, uint progress)
        {
            run.Progress = progress;
            OnInstanceProgressUpdate(run);
        }

        // CInstance::ClearEntities: the living chars, mobs, pets and trusts drop what they were doing
        private void ClearInstanceEntities(Instance run)
        {
            foreach (var c in InstanceChars(run))
                if (c.IsAlive) ClearInstanceEntityStates(c);
            foreach (var m in run.Info.Mobs)
                if (m.IsAlive) ClearInstanceEntityStates(m);
            foreach (var p in run.Info.Pets)
                if (p.IsAlive) ClearInstanceEntityStates(p);
            foreach (var t in run.Info.Trusts)
                if (t.IsAlive) ClearInstanceEntityStates(t);
        }

        // PAI->ClearStateStack: no engagement, no cast or ability under way
        private void ClearInstanceEntityStates(BattleEntity b)
        {
            AbortMagic(b, NowMs());
            if (b is CharData c)
            {
                CancelAbility(c);
                if (c.BattleTarget != null) Disengage(c);
            }
            else if (b is MobEntity m) ResetCombat(m);
        }

        // CInstance::Fail / Complete / Cancel
        private void FailInstance(Instance run)
        {
            run.Status = InstanceStatus.Failed;
            ClearInstanceEntities(run);
            OnInstanceFailure(run);
        }

        private void CompleteInstance(Instance run)
        {
            run.Status = InstanceStatus.Complete;
            ClearInstanceEntities(run);
            OnInstanceComplete(run);
        }

        // CInstance::CheckTime: onInstanceTimeUpdate each second (every 40 s while nobody ever entered), not once failed
        private void CheckInstanceTime(Instance run, long now)
        {
            long frequency = 1000;
            if (InstanceChars(run).Count == 0 && run.StartMs == run.LastTimeCheckMs) frequency = 40_000;
            if (run.LastTimeCheckMs + frequency <= now && !run.Failed)
            {
                OnInstanceTimeUpdate(run, (uint)(Math.Max(0, now - run.StartMs) / 1000));
                run.LastTimeCheckMs = now;
            }
        }

        // CZoneInstance::ZoneServer's instance half: each run's time check, then the runs failed or completed and empty go
        private void TickInstances()
        {
            long now = NowMs();
            foreach (var (zone, runs) in instances_)
            {
                foreach (var run in runs.ToArray())
                {
                    CheckInstanceTime(run, now);
                    if ((run.Failed || run.Completed) && InstanceChars(run).Count == 0)
                    {
                        log_($"[CZoneInstance] ZoneServer cleaned up Instance {run.Data.Name}");
                        runs.Remove(run);
                    }
                }
            }
            CheckInstanceLoadQueue();
        }

        // ---- CZoneInstance::IncreaseZoneCounter / DecreaseZoneCounter -------------------------------------------------

        // before the char takes an act index in an instanced zone: back into the run it is registered to (a zone change
        // or a reconnect makes a new char), else out to onInstanceLoadFailed's zone (or the previous one) at 0,0,0.
        // False when the char was moved out. A run lives on the process holding its zone: a char registered there
        // finds it again coming from another process, one that is not is put out.
        private bool InstanceCharAttach(CharData c)
        {
            if (!IsInstancedZone(Zone(c.Zone).Info)) return true;
            if (EffectiveInstance(c.Instance, c.Zone) == null && instances_.TryGetValue(c.Zone, out var runs))
                foreach (var run in runs)
                    if (run.RegisteredChars.Contains(c.Id)) c.Instance = run;
            if (EffectiveInstance(c.Instance, c.Zone) != null) return true;

            log_($"Failed to place {c.Name} in {Zone(c.Zone).Info.Name} ({c.Zone}). Placing them in that zone's instance exit area.");
            ushort exit = OnInstanceLoadFailed(Zone(c.Zone).Info);
            ushort from = c.Zone;
            // At this stage, can only send the player to a zone on this map server: the exit zone when this process
            // holds it (zoneutils::GetZone), else the previous one
            (c.X, c.Y, c.Z) = (0, 0, 0);
            c.Zone = exit != 0 && exit != ZoneNoDestination && IsZoneOnThisProcess(exit) ? exit : c.PrevZone;
            c.PrevZone = from;
            SavePosition(c);
            return false;
        }

        // the rest of the instanced IncreaseZoneCounter once the char is in: onInstanceZoneIn, the level cap, and on its
        // first entry the run's entry position and afterInstanceRegister 400 ms on
        private void InstanceCharZoneIn(CharData c)
        {
            var run = InstanceOf(c);
            if (run == null) return;
            OnInstanceZoneIn(c, run);
            if (run.LevelCap > 0)
                c.StatusEffects.AddStatusEffect(new StatusEffect(EffectLevelRestriction, EffectLevelRestriction, run.LevelCap, 0, 0));
            if (run.EnteredChars.Add(c.Id))
            {
                (c.X, c.Y, c.Z, c.Rotation) = (run.EntryX, run.EntryY, run.EntryZ, run.EntryRot);
                QueueNative(c, 400, false, e => AfterInstanceRegister((CharData)e));
            }
        }

        // CZoneInstance::DecreaseZoneCounter's instance half: the level restriction off, out of the run; an emptied
        // run that is still going marks its wipe time
        private void InstanceCharZoneOut(CharData c)
        {
            var run = InstanceOf(c);
            if (run == null) return;
            c.StatusEffects.DelStatusEffectSilent(EffectLevelRestriction);
            c.Instance = null;
            if (InstanceChars(run).Count == 0 && !(run.Failed || run.Completed)) run.WipeAtMs = NowMs();
        }

        // ---- the luautils hooks ---------------------------------------------------------------------------------------

        // a function of the run's cached instance script, with the arguments pushed by push; false when it is missing
        private bool CallInstanceHook(Instance run, string name, string where, Action push, int nargs)
        {
            if (lua_ == null || scripts_ == null) return false;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(run.Data.Filename, name);
                if (lua_.IsNil(-1)) return false;
                push();
                lua_.Call(nargs, 0);
                return true;
            }
            catch (LuaException e)
            {
                log_($"luautils::{where} {e.Message}");
                return true;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        private void PushInstance(Instance run) => lua_.PushObject(run, InstanceLuaType);

        // luautils::AfterInstanceRegister
        private void AfterInstanceRegister(CharData c)
        {
            var run = c.Instance;
            if (run == null)
            {
                log_("PInstance is null.");
                return;
            }
            CallInstanceHook(run, "afterInstanceRegister", "afterInstanceRegister", () => lua_.PushObject(c), 1);
        }

        // luautils::OnInstanceTimeUpdate / OnInstanceFailure / OnInstanceCreated / OnInstanceProgressUpdate /
        // OnInstanceComplete
        private void OnInstanceTimeUpdate(Instance run, uint seconds) =>
            CallInstanceHook(run, "onInstanceTimeUpdate", "onInstanceTimeUpdate", () => { PushInstance(run); lua_.Push((double)seconds); }, 2);

        private void OnInstanceFailure(Instance run) =>
            CallInstanceHook(run, "onInstanceFailure", "onInstanceFailure", () => PushInstance(run), 1);

        private void OnInstanceCreated(Instance run) =>
            CallInstanceHook(run, "onInstanceCreated", "onInstanceCreated", () => PushInstance(run), 1);

        private void OnInstanceProgressUpdate(Instance run) =>
            CallInstanceHook(run, "onInstanceProgressUpdate", "onInstanceProgressUpdate", () => { PushInstance(run); lua_.Push((double)run.Progress); }, 2);

        private void OnInstanceComplete(Instance run) =>
            CallInstanceHook(run, "onInstanceComplete", "onInstanceComplete", () => PushInstance(run), 1);

        // luautils::OnInstanceCreatedCallback: the script must have it
        private void OnInstanceCreatedCallback(CharData requester, Instance run)
        {
            if (!CallInstanceHook(run, "onInstanceCreatedCallback", "OnInstanceCreatedCallback",
                                  () => { lua_.PushObject(requester); PushInstance(run); }, 2))
                log_("luautils::OnInstanceCreatedCallback: undefined procedure onInstanceCreatedCallback");
        }

        // luautils::OnInstanceZoneIn: the instanced zone's own onInstanceZoneIn
        private void OnInstanceZoneIn(CharData c, Instance run)
        {
            if (!ZoneLuaEnabled || lua_ == null) return;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(ZoneLuaFile(Zone(run.Zone).Info), "onInstanceZoneIn");
                if (lua_.IsNil(-1)) return;
                lua_.PushObject(c);
                PushInstance(run);
                lua_.Call(2, 0);
            }
            catch (LuaException e)
            {
                log_($"luautils::onInstanceZoneIn {e.Message}");
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnInstanceLoadFailed: the zone to put the char in, ZONE_NO_DESTINATION without the hook, 0 on an
        // error or a non-number
        private ushort OnInstanceLoadFailed(ZoneInfo info)
        {
            if (lua_ == null || scripts_ == null) return ZoneNoDestination;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(ZoneLuaFile(info), "onInstanceLoadFailed");
                if (lua_.IsNil(-1)) return ZoneNoDestination;
                lua_.Call(0, 1);
                return lua_.IsNumber(-1) ? (ushort)lua_.ToEnum(-1) : (ushort)0; // get<xi::ZoneId>
            }
            catch (LuaException e)
            {
                log_($"luautils::onInstanceLoadFailed {e.Message}");
                return 0;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // ---- the entity lookups by instance ---------------------------------------------------------------------------

        // CZoneEntities::GetEntity(targid, filter) for a run: mobs / NPCs below 0x400, chars below 0x700, then the pets,
        // trusts, NPCs and mobs with a dynamic targid
        private object InstanceEntity(Instance run, ushort targid, byte filter)
        {
            var info = run.Info;
            if (targid < 0x400)
            {
                if ((filter & TypeMob) != 0 && info.Mobs.Find(m => m.TargId == targid) is { } mob) return mob;
                if ((filter & TypeNpc) != 0 && info.Npcs.Find(n => n.TargId == targid) is { } npc) return npc;
            }
            else if (targid < 0x700)
            {
                if ((filter & TypePc) != 0)
                    foreach (var c in InstanceChars(run))
                        if (c.TargId == targid) return c;
            }
            else if (targid < 0x1000)
            {
                if ((filter & TypePet) != 0 && info.Pets.Find(p => p.TargId == targid) is { } pet) return pet;
                if ((filter & TypeTrust) != 0 && info.Trusts.Find(t => t.TargId == targid) is { } trust) return trust;
                if ((filter & TypeNpc) != 0 && info.Npcs.Find(n => n.TargId == targid) is { } dynNpc) return dynNpc;
                if ((filter & TypeMob) != 0 && info.Mobs.Find(m => m.TargId == targid) is { } dynMob) return dynMob;
            }
            return null;
        }

        // the CLuaInstance an argument holds, or null
        private static Instance InstanceArg(LuaState l, int idx) => l.ToObject(idx) as Instance;

        // ---- CLuaInstance ---------------------------------------------------------------------------------------------

        private static Instance LuaInstance(LuaState l) =>
            l.ToObject(1) as Instance ?? throw new InvalidOperationException("bad self: expected " + InstanceLuaType);

        private void RegisterInstanceBindings(Dictionary<string, LuaFunction> m)
        {
            lua_.RegisterType(InstanceLuaType, new Dictionary<string, LuaFunction>
            {
                ["getID"] = l => Ret(l, LuaInstance(l).Id),
                ["getName"] = l =>
                {
                    l.PushString(LuaInstance(l).Data.Name);
                    return 1;
                },
                ["getZone"] = l =>
                {
                    l.PushObject(Zone(LuaInstance(l).Zone).Info, WeatherZoneLuaType);
                    return 1;
                },
                ["getEntranceZoneID"] = l => Ret(l, LuaInstance(l).Data.EntranceZone),
                ["setLevelCap"] = l =>
                {
                    LuaInstance(l).LevelCap = (byte)l.ToIntArg(2);
                    return 0;
                },
                ["getAllies"] = l => ZoneLuaPushArray(l, LuaInstance(l).Allies),
                ["getChars"] = l => ZoneLuaPushArray(l, InstanceChars(LuaInstance(l))),
                ["getMobs"] = l => ZoneLuaPushArray(l, LuaInstance(l).Info.Mobs),
                ["getNpcs"] = l => ZoneLuaPushArray(l, LuaInstance(l).Info.Npcs),
                ["getPets"] = l => ZoneLuaPushArray(l, LuaInstance(l).Info.Pets),
                ["getTimeLimit"] = l => Ret(l, (uint)(LuaInstance(l).TimeLimitMs / 1000)),
                ["setTimeLimit"] = l =>
                {
                    LuaInstance(l).TimeLimitMs = l.ToUIntArg(2) * 1000L;
                    return 0;
                },
                ["getEntryPos"] = l =>
                {
                    var run = LuaInstance(l);
                    l.NewTable();
                    l.Push((double)run.EntryX);
                    l.SetField(-2, "x");
                    l.Push((double)run.EntryY);
                    l.SetField(-2, "y");
                    l.Push((double)run.EntryZ);
                    l.SetField(-2, "z");
                    l.Push((double)run.EntryRot);
                    l.SetField(-2, "rot");
                    return 1;
                },
                ["getLevelCap"] = l => Ret(l, LuaInstance(l).LevelCap),
                ["getLastTimeUpdate"] = l => Ret(l, (uint)(LuaInstance(l).LastTimeUpdateMs / 1000)),
                ["setLastTimeUpdate"] = l =>
                {
                    LuaInstance(l).LastTimeUpdateMs = l.ToUIntArg(2) * 1000L;
                    return 0;
                },
                ["getProgress"] = l => Ret(l, LuaInstance(l).Progress),
                ["setProgress"] = l =>
                {
                    SetInstanceProgress(LuaInstance(l), l.ToUIntArg(2));
                    return 0;
                },
                ["getEntity"] = l =>
                {
                    byte filter = l.IsNumber(3) ? (byte)l.ToIntArg(3) : (byte)0xFF;
                    l.PushObject(InstanceEntity(LuaInstance(l), (ushort)l.ToIntArg(2), filter));
                    return 1;
                },
                // getWipeTime: the elapsed seconds when the run emptied; setWipeTime sets it
                ["getWipeTime"] = l =>
                {
                    var run = LuaInstance(l);
                    return Ret(l, (uint)((run.WipeAtMs - run.StartMs) / 1000));
                },
                ["setWipeTime"] = l =>
                {
                    var run = LuaInstance(l);
                    run.WipeAtMs = run.StartMs + l.ToUIntArg(2) * 1000L;
                    return 0;
                },
                ["getStage"] = l => Ret(l, LuaInstance(l).Stage),
                ["setStage"] = l =>
                {
                    LuaInstance(l).Stage = l.ToUIntArg(2);
                    return 0;
                },
                ["fail"] = l =>
                {
                    FailInstance(LuaInstance(l));
                    return 0;
                },
                ["failed"] = l => Ret(l, LuaInstance(l).Failed),
                ["complete"] = l =>
                {
                    CompleteInstance(LuaInstance(l));
                    return 0;
                },
                ["completed"] = l => Ret(l, LuaInstance(l).Completed),
                // insertAlly(groupid): a mob_groups ally in the run
                ["insertAlly"] = l =>
                {
                    var run = LuaInstance(l);
                    uint groupId = l.ToUIntArg(2);
                    var ally = InstantiateAlly(groupId, run.Zone, run);
                    if (ally == null) log_($"CLuaBattlefield::insertAlly - group ID {groupId} not found!");
                    else run.Allies.Add(ally);
                    l.PushObject(ally);
                    return 1;
                },
                // insertDynamicEntity(table): CLuaZone's, in the run
                ["insertDynamicEntity"] = l =>
                {
                    var run = LuaInstance(l);
                    if (!l.IsTable(2)) throw new LuaArgumentException(2, "table", l.Describe(2));
                    l.PushObject(GenerateDynamicEntity(run.Info, l, 2, run));
                    return 1;
                },
                ["getLocalVar"] = l => Ret(l, LuaInstance(l).LocalVars.TryGetValue(l.ToStringArg(2) ?? "", out var v) ? v : 0),
                ["setLocalVar"] = l =>
                {
                    LuaInstance(l).LocalVars[l.ToStringArg(2) ?? ""] = (ulong)l.ToIntegralArg(3); // uint64_t
                    return 0;
                },
            });

            // GetCachedInstanceScript(id): the instance's script object, nil (with the error) when it is not cached
            lua_.SetGlobal("GetCachedInstanceScript", l =>
            {
                var data = InstanceDataOf((ushort)l.ToIntArg(1));
                if (!PushLuaObjectFromFilename(data.Filename))
                {
                    log_($"luautils::GetCachedInstanceScript: Could not retrieve Lua object for instance {l.ToIntArg(1)}");
                    l.PushNil();
                }
                return 1;
            });

            // createInstance(id): queued, loaded at the end of a tick
            m["createInstance"] = l =>
            {
                if (Pc(l, "createInstance") is not CharData c) return 0;
                ushort id = (ushort)l.ToIntArg(2);
                if (!IsValidInstanceId(id))
                {
                    log_($"CLuaBaseEntity::createInstance: Invalid instanceID: {id}");
                    return 0;
                }
                instanceLoadQueue_.Enqueue((c.Id, id));
                return 0;
            };
            // instanceEntry(npc, response): GP_SERV_COMMAND_REGISTRATION
            m["instanceEntry"] = l =>
            {
                if (Pc(l, "instanceEntry") is not CharData c) return 0;
                PushTo(c, RegistrationPacket(TargIdOf(l.ToObject(2)), l.ToUIntArg(3)));
                return 0;
            };
            // setInstance(instance): the entity's instance; a char is registered to it
            m["setInstance"] = l =>
            {
                if (Pc(l, "setInstance") is not CharData c) return 0;
                var run = InstanceArg(l, 2);
                c.Instance = run;
                if (run != null) RegisterInstanceChar(run, c);
                return 0;
            };
            m["getInstance"] = l =>
            {
                var run = Entity(l) switch
                {
                    BattleEntity b => b.Instance,
                    NpcEntity n => n.Instance,
                    _ => null,
                };
                if (run == null) l.PushNil();
                else PushInstance(run);
                return 1;
            };
        }

        // 0x0BF GP_SERV_COMMAND_REGISTRATION: Result u16 @6, ActIndex u32 @12
        private static BasicPacket RegistrationPacket(ushort targid, uint response)
        {
            var w = new PacketWriter(0x0BF, 28);
            w.U16(6, (ushort)response);
            w.U32(12, targid);
            return w.Packet;
        }

        // GetLuaObjectFromFilename: push xi.<path>, false (nothing pushed) when it is not a table
        private bool PushLuaObjectFromFilename(string filename)
        {
            if (lua_ == null || string.IsNullOrEmpty(filename)) return false;
            var parts = filename.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
            int scriptsAt = parts.IndexOf("scripts");
            if (scriptsAt < 0) return false;
            parts.RemoveRange(0, scriptsAt + 1);
            string last = parts[^1];
            int dot = last.LastIndexOf('.');
            if (dot > 0) parts[^1] = last.Substring(0, dot);
            int top = lua_.Top;
            lua_.GetGlobal("xi");
            foreach (var part in parts)
            {
                if (!lua_.IsTable(-1)) break;
                lua_.GetField(-1, part);
                lua_.Remove(-2);
            }
            if (!lua_.IsTable(-1))
            {
                lua_.SetTop(top);
                return false;
            }
            return true;
        }
    }
}
