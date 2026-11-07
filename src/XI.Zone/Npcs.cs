using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using XI.Map;

namespace XI.Zone
{
    // data/enums/<name>.yaml: the yaml tokens (status: cutscene_only, animation: close_door, content: cop) and their
    // values, as yaml::resolveEnum reads them
    public sealed class DataEnums
    {
        private readonly string dir_;
        private readonly Dictionary<string, Dictionary<string, int>> enums_ = new Dictionary<string, Dictionary<string, int>>();

        public DataEnums(string dataDir) => dir_ = Path.Combine(dataDir, "enums");

        public Dictionary<string, int> Of(string name)
        {
            if (enums_.TryGetValue(name, out var values)) return values;
            values = new Dictionary<string, int>();
            var path = Path.Combine(dir_, name + ".yaml");
            if (File.Exists(path))
            {
                var map = MiniYaml.Load(path).MapOrNull("values");
                if (map != null)
                    foreach (var (k, v) in map) values[k] = ParseInt((string)v);
            }
            enums_[name] = values;
            return values;
        }

        // a decimal value, or a 0x-prefixed hex one (the flags enums)
        public static int ParseInt(string v) =>
            v.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? int.Parse(v.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : int.Parse(v, CultureInfo.InvariantCulture);

        public int Resolve(string name, string token, int fallback = 0)
        {
            if (token == null) return fallback;
            return Of(name).TryGetValue(token, out var v) ? v : throw new FormatException($"unknown {name} '{token}'");
        }
    }

    // MODELTYPE: look_t::size, which the 0x00E SubKind follows
    public enum ModelType : ushort
    {
        Standard = 0, Equipped = 1, Door = 2, Elevator = 3, Ship = 4, Unk5 = 5, Automaton = 6, Chocobo = 7,
    }

    // A static CNpcEntity as zoneutils::InsertNPCs builds it from npcs.yaml. Its script (scripts/zones/<zone>/npcs/<name>.lua)
    // answers onTrigger and the events it starts; luautils::OnEntityLoad caches it (LoadZoneScripts, ZoneHost.Events.cs)
    // and OnNpcSpawn runs its onSpawn (ZoneHost.NpcAi.cs).
    public sealed class NpcEntity : IPathMover
    {
        public const byte StatusNormal = 0, StatusUpdate = 1, StatusDisappear = 2;
        public const uint DefaultEntityFlags = 0x003;

        public uint Id;
        public ushort TargId;
        public Instance Instance; // PInstance: the instance it is in, null outside one
        public string Name = "";        // the script name (CBaseEntity::name, what getName() returns)
        public string PacketName = "";  // display_name
        public float X, Y, Z;
        public ushort Moving;
        public byte Rotation;
        public ushort[] Look = new ushort[10];  // look_t laid flat: size, modelid, head .. ranged
        public ushort LookAt;
        public byte Animation, AnimationSub;
        public byte Status = StatusDisappear;
        public byte NameVis;
        public uint EntityFlags = DefaultEntityFlags;
        public byte SpawnAnimation; // xi::SpawnAnimation: 1 Special (setSpawnAnimation)
        public byte NamePrefix;
        public uint? DoorId;
        public byte ModelSize;
        public float ModelHitboxSize;
        public byte Speed = 50, AnimationSpeed = 50;
        public bool Widescan, AlwaysRelevant, ContentDisabled;
        public bool Triggerable; // triggerable_: a dynamic NPC given an onTrigger (setTriggerable), flagged in its 0x00E
        public bool IsRenamed, PriorityRender; // isRenamed (renameEntity), priorityRender (CliPriorityFlag)
        public ElevatorData Elevator;           // npcs.yaml's elevator: a lift's platform (ZoneHost.Elevators.cs)

        // CBaseEntity::m_localVars
        public readonly Dictionary<string, uint> LocalVars = new Dictionary<string, uint>();
        public readonly AiEventHandler Events = new AiEventHandler();
        // CBaseEntity::PBattlefield (ZoneHost.Battlefield.cs)
        public Battlefield PBattlefield;
        // m_bReleaseTargIDOnDisappear: a dynamic NPC leaves the zone once it disappears (ZoneHost.DynamicEntities.cs)
        public bool ReleaseTargIdOnDisappear;

        // CTriggerState for a door: when it swings shut again (0 while closed)
        public long DoorCloseAtMs;

        // PAI->PathFind from initNpcAi (ZoneHost.NpcAi.cs), the next logic tick, and the updatemask PostTick sends at
        // most every 250 ms
        public PathFind PathFind;
        public long NextAiTickMs, NextUpdateMs;
        public byte UpdateMask;
        public int TriggerHoldTicks; // the logic ticks a CTriggerState still holds the path
        public InactiveState Inactive; // the CInactiveState wait puts on it (ZoneHost.NpcAi.cs)

        float IPathMover.X { get => X; set => X = value; }
        float IPathMover.Y { get => Y; set => Y = value; }
        float IPathMover.Z { get => Z; set => Z = value; }
        byte IPathMover.Rotation { get => Rotation; set => Rotation = value; }
        ushort IPathMover.Moving { get => Moving; set => Moving = value; }
        byte IPathMover.Speed => Speed;
        byte IPathMover.Animation => Animation;
        BattleEntity IPathMover.PathBattleTarget => null;
        void IPathMover.MarkPositionDirty() => UpdateMask |= EntityUpdatePacket.UpdatePos;
        float IPathMover.PathHitboxRadius => ModelHitboxSize * 0.5f;

        public ModelType Model => (ModelType)Look[0];

        // npcs.yaml's npc map, ordered by id. contentEnabled is luautils::IsContentEnabled for a content slug.
        public static List<NpcEntity> Load(string path, DataEnums enums, Func<string, bool> contentEnabled)
        {
            var npcs = new List<NpcEntity>();
            if (!File.Exists(path)) return npcs;
            var doc = MiniYaml.Load(path).MapOrNull("npcs");
            if (doc == null) return npcs;
            foreach (var (key, value) in doc)
            {
                if (value is not Dictionary<string, object> src) continue;
                uint id = uint.Parse(key, CultureInfo.InvariantCulture);
                var render = src.MapOrNull("render") ?? new Dictionary<string, object>();
                var at = src.List("at").ConvertAll(o => float.Parse((string)o, CultureInfo.InvariantCulture));
                if (at.Count != 0 && (at.Count < 3 || at.Count > 4)) throw new FormatException($"npc {id} position needs three values plus an optional facing");

                var npc = new NpcEntity
                {
                    Id = id,
                    TargId = (ushort)(id & 0xFFF),
                    Name = src.Str("script", ""),
                    PacketName = src.Str("display_name", ""),
                    X = at.Count > 0 ? at[0] : 0, Y = at.Count > 0 ? at[1] : 0, Z = at.Count > 0 ? at[2] : 0,
                    Rotation = at.Count > 3 ? (byte)at[3] : (byte)0,
                    Moving = U16(render.Str("moving")),
                    Look = render.MapOrNull("look") is { } look ? LookFields(look, id) : new ushort[10],
                    LookAt = U16(src.Str("look_at")),
                    Animation = (byte)enums.Resolve("animation", render.Str("animation")),
                    AnimationSub = (byte)U16(render.Str("animation_sub")),
                    Status = (byte)enums.Resolve("status", src.Str("status"), StatusDisappear),
                    NameVis = (byte)U16(render.Str("name_vis")),
                    EntityFlags = render.Str("entity_flags") is { } f ? uint.Parse(f, CultureInfo.InvariantCulture) : DefaultEntityFlags,
                    NamePrefix = (byte)U16(render.Str("name_prefix")),
                    DoorId = render.Str("door_id") is { } d ? uint.Parse(d, CultureInfo.InvariantCulture) : null,
                    ModelSize = (byte)U16(render.Str("model_size")),
                    ModelHitboxSize = MathF.Max(0.0f, U16(render.Str("hitbox")) / 10.0f),
                    Speed = (byte)(src.Str("speed") is { } sp ? U16(sp) : 50),
                    AnimationSpeed = (byte)(src.Str("animation_speed") is { } asp ? U16(asp) : 50),
                    Widescan = src.Str("widescan") == "true",
                    AlwaysRelevant = render.Str("king") == "true",
                    Elevator = ElevatorData.Load(src.MapOrNull("elevator"), id, enums),
                };

                string content = src.Str("content");
                if (content != null)
                {
                    enums.Resolve("content", content);
                    if (!contentEnabled(content))
                    {
                        npc.X = npc.Y = npc.Z = 0;
                        npc.Status = StatusDisappear;
                        npc.Widescan = false;
                        npc.ContentDisabled = true;
                    }
                }
                npcs.Add(npc);
            }
            npcs.Sort((a, b) => a.Id.CompareTo(b.Id));
            return npcs;
        }

        // shared::toLookFields
        internal static ushort[] LookFields(Dictionary<string, object> src, uint id)
        {
            var look = new ushort[10];
            string type = src.Str("type", "");
            if (type == "equipped" || type == "chocobo")
            {
                look[0] = (ushort)(type == "equipped" ? ModelType.Equipped : ModelType.Chocobo);
                look[1] = (ushort)(U16(src.Str("face")) | U16(src.Str("race")) << 8);
                look[2] = U16(src.Str("head"));
                look[3] = U16(src.Str("body"));
                look[4] = U16(src.Str("hands"));
                look[5] = U16(src.Str("legs"));
                look[6] = U16(src.Str("feet"));
                look[7] = U16(src.Str("main"));
                look[8] = U16(src.Str("sub"));
                look[9] = U16(src.Str("ranged"));
                return look;
            }
            look[0] = type switch
            {
                "standard" => 0, "door" => 2, "elevator" => 3, "ship" => 4, "misc" => 5, "automaton" => 6,
                _ => throw new FormatException($"'{id}' declares unknown look type '{type}'"),
            };
            look[1] = U16(src.Str("model"));
            return look;
        }

        internal static ushort U16(string s) => s == null ? (ushort)0 : ushort.Parse(s, CultureInfo.InvariantCulture);
    }

    // CEntityUpdatePacket (GP_SERV_COMMAND_CHAR_NPC, 0x00E) for an NPC
    public static partial class EntityUpdatePacket
    {
        public const byte UpdatePos = 0x01, UpdateStatus = 0x02, UpdateHp = 0x04, UpdateName = 0x08, UpdateAllMob = 0x0F;
        public const byte UpdateCombat = 0x07; // UPDATE_COMBAT: pos, status and HP
        private const int PacketNameLength = 16;

        public static BasicPacket Npc(NpcEntity npc, EntityUpdate type, byte updatemask)
        {
            var w = new PacketWriter(0x00E, 0x58);
            w.U32(0x04, npc.Id);
            UpdateWith(w.Packet, npc, type, updatemask);
            return w.Packet;
        }

        // CEntityUpdatePacket::updateWith: an update lands on top of what the packet already holds, so a despawn
        // merged into a pending spawn keeps the spawn's fields
        public static void UpdateWith(BasicPacket packet, NpcEntity npc, EntityUpdate type, byte updatemask)
        {
            var w = new PacketWriter(packet);
            var b = packet.Buffer;
            w.U16(0x08, npc.TargId);
            b[0x0A] |= updatemask;

            switch (type)
            {
                case EntityUpdate.Despawn:
                    b[0x1F] = 0x02; // despawn animation
                    b[0x0A] = 0x30;
                    updatemask = UpdateAllMob;
                    break;
                case EntityUpdate.Spawn:
                    updatemask = UpdateAllMob;
                    if (npc.Model == ModelType.Equipped || npc.Model == ModelType.Chocobo) updatemask = 0x57;
                    if (npc.AnimationSub != 0) b[0x2A] = 4;
                    if (npc.SpawnAnimation == 1) b[0x28] |= 0x04;
                    b[0x0A] = updatemask;
                    break;
            }

            if ((updatemask & UpdatePos) != 0)
            {
                b[0x0B] = npc.Rotation;
                w.F32(0x0C, npc.X);
                w.F32(0x10, npc.Y);
                w.F32(0x14, npc.Z);
                w.U16(0x18, npc.Moving);
                w.U16(0x1A, npc.LookAt << 1);
                b[0x1C] = npc.Speed;
                b[0x1D] = npc.AnimationSpeed;
            }

            // allegiance is Mob, so the status goes out as it is
            b[0x20] = npc.Status;

            if ((updatemask & UpdateHp) != 0)
            {
                b[0x1E] = 0x64; // HPP: 100
                b[0x1F] = npc.Animation;
                b[0x2A] |= npc.AnimationSub;
                w.U32(0x21, npc.EntityFlags);
                b[0x27] = npc.NamePrefix;
                if (npc.Triggerable) b[0x28] |= 0x40;
                b[0x29] = 0; // allegiance: mob
                b[0x2B] = npc.NameVis;
            }

            if (npc.PriorityRender) b[0x28] |= 0x20;

            // a ship or lift puts its door id and phase stamp where a plain NPC puts its name
            bool isTransport = npc.Model == ModelType.Elevator || npc.Model == ModelType.Ship;
            if ((updatemask & UpdateName) != 0 || isTransport)
            {
                w.SetSize(0x48);
                if (isTransport) w.Bytes(0x34, TransportName(npc));
                else w.Name(0x34, npc.Name, PacketNameLength);
            }

            switch (npc.Model)
            {
                case ModelType.Standard:
                case ModelType.Unk5:
                case ModelType.Automaton:
                    w.U32(0x30, (uint)(npc.Look[0] | npc.Look[1] << 16));
                    break;
                case ModelType.Equipped:
                case ModelType.Chocobo:
                    w.SetSize(0x48);
                    for (int k = 0; k < 10; k++) w.U16(0x30 + k * 2, npc.Look[k]);
                    break;
                case ModelType.Door:
                    w.SetSize(0x48);
                    w.U16(0x30, npc.Look[0]);
                    if (npc.DoorId is uint door) w.U32(0x34, door);
                    else w.Name(0x34, npc.Name, PacketNameLength);
                    break;
                case ModelType.Ship:
                case ModelType.Elevator:
                    w.SetSize(0x48);
                    w.U16(0x30, npc.Look[0]);
                    w.Bytes(0x34, TransportName(npc));
                    break;
            }

            if (npc.IsRenamed) WriteRenamed(w, npc.TargId, npc.PacketName, updatemask);
        }

        // a renamed entity sends its name with every update, or the client puts the old one back: UPDATE_NAME, the
        // size to 0x48, and the name after a 0x01 marker for a static entity's act index
        private static void WriteRenamed(PacketWriter w, ushort targId, string name, byte updatemask)
        {
            var b = w.Packet.Buffer;
            b[0x0A] |= (byte)(updatemask | UpdateName);
            w.SetSize(0x48);
            int nameOffset = 0x34;
            if (targId < 1024)
            {
                w.U16(0x34, 1);
                nameOffset = 0x35;
            }
            Array.Clear(b, nameOffset, b.Length - nameOffset);
            var bytes = System.Text.Encoding.ASCII.GetBytes(name);
            Array.Copy(bytes, 0, b, nameOffset, Math.Min(Math.Min(bytes.Length, PacketNameLength), b.Length - nameOffset));
        }

        // getTransportNPCName: the door id (or the name's first four bytes), then the TransportTimestamp local var
        // (the ship's or the lift's leg start), and for a lift the TransportTravel byte
        private static byte[] TransportName(NpcEntity npc)
        {
            bool isElevator = npc.Model == ModelType.Elevator;
            var str = new byte[isElevator ? 10 : 8];
            if (npc.DoorId is uint door) BitConverter.GetBytes(door).CopyTo(str, 0);
            else
            {
                var name = System.Text.Encoding.ASCII.GetBytes(npc.Name);
                Array.Copy(name, str, Math.Min(name.Length, 4));
            }
            BitConverter.GetBytes(npc.LocalVars.TryGetValue("TransportTimestamp", out uint stamp) ? stamp : 0u).CopyTo(str, 4);
            if (isElevator) str[8] = (byte)(npc.LocalVars.TryGetValue("TransportTravel", out uint travel) ? travel : 0u);
            return str;
        }
    }
}
