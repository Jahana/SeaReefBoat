using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // The map server's game side: a character logs in (0x00A), finishes loading (0x011 / 0x00C), walks around (0x015),
    // sees the zone's static NPCs and talks to them (0x01A / 0x05B, ZoneHost.Events). The packets are the verified
    // LoginPackets / GameOkPackets; there are no mobs or effects yet, and every other client packet is ignored.
    public sealed partial class ZoneHost
    {
        private readonly IDatabase db_;
        private readonly CharLoader loader_;
        private readonly string dataDir_;
        private readonly MeritTable merits_;
        private readonly bool enableRoe_;
        private readonly Action<string> log_;
        private readonly DataEnums enums_;
        private readonly Func<string, bool> contentEnabled_;
        private readonly Dictionary<ushort, (ZoneInfo Info, uint Ip, ushort Port)> zones_ = new Dictionary<ushort, (ZoneInfo, uint, ushort)>();
        private Dictionary<string, ushort> zoneIds_;

        public Func<long> UnixNow = XI.Common.EarthTime.UnixSec;

        // contentEnabled is luautils::IsContentEnabled for a content slug (cop, toau, ...); null enables everything
        public ZoneHost(IDatabase db, CharLoader loader, string dataDir, bool enableRoe, Action<string> log = null, Func<string, bool> contentEnabled = null)
        {
            db_ = db;
            loader_ = loader;
            if (loader_ != null)
                loader_.AfterGiftMods = c =>
                {
                    LoadSetSpells(c);
                    LoadAutomaton(c);
                };
            if (loader_ != null) loader_.BuildTraits = BuildCharTraits;
            if (loader_ != null) loader_.BeforeEquip = LoadStatusEffects;
            if (loader_ != null) loader_.AfterLevels = LoadCharRecasts;
            if (loader_ != null) loader_.MonTable = MonTable;
            if (loader_?.Stats != null) loader_.Stats.MonTable = MonTable;
            if (loader_ != null) loader_.AbilityLearned = AbilityLearnedRecast;
            if (loader_?.Stats != null) loader_.Stats.UnixNow = () => UnixNow();
            if (loader_ != null)
            {
                loader_.Progress.UnixNow = () => UnixNow();
                loader_.Progress.Log = s => log_(s);
            }
            if (loader_?.Stats != null) loader_.Stats.ChargedItemEquipChanged = ChargedItemRecast;
            GameOkPackets.AttrOverride = ItemAttrOverride;
            if (loader_?.Stats != null) loader_.Stats.ItemEquipScript = OnItemEquipScript;
            if (loader_?.Stats != null) loader_.Stats.Items.Log ??= log_;
            if (loader_?.Stats != null) loader_.Stats.PersonalChocoboGearChanged = PersonalChocoboGearChanged;
            if (loader_?.Stats?.Settings is { } speeds)
            {
                MountUtils.SpeedLimit = (byte)speeds.SpeedLimit;
                MountUtils.MountSpeed = (byte)speeds.MountSpeed;
            }
            dataDir_ = dataDir;
            NavMeshDir = Path.GetFullPath(Path.Combine(dataDir, "..", "navmeshes"));
            XiMeshDir = Path.GetFullPath(Path.Combine(dataDir, "..", "ximeshes"));
            enableRoe_ = enableRoe;
            log_ = log ?? (_ => { });
            contentEnabled_ = contentEnabled ?? (_ => true);
            enums_ = new DataEnums(dataDir);
            // the loader counts each char's upgrades against the same table
            merits_ = loader_.MeritTable ??= MeritTable.Load(dataDir, enums_);
            effectTable_ = loader_.EffectTable ??= EffectTable.Load(dataDir, enums_);
        }

        private ResultSet Exec(string sql, params BoundValue[] args) => db_.Execute(sql, args);

        // zone_settings name + zoneip and data/zones/<name>/zone.yaml, then the zone's weather (ZoneHost.Weather)
        private (ZoneInfo Info, uint Ip, ushort Port) Zone(ushort id)
        {
            if (zones_.TryGetValue(id, out var z)) return z;
            var r = Exec("SELECT name, zoneip, zoneport FROM zone_settings WHERE zoneid = ?", BoundValue.Of(id));
            string name = "", ip = "0.0.0.0";
            ushort port = 0;
            if (r != null && r.Next())
            {
                name = r.Get<string>("name");
                ip = r.Get<string>("zoneip");
                port = r.Get<ushort>("zoneport");
            }
            var info = ZoneInfo.Load(Path.Combine(dataDir_, "zones"), id, name, ZoneIdByName);
            info.LoadNpcs(Path.Combine(dataDir_, "zones"), enums_, contentEnabled_);
            AddElevators(info);
            if (info.Npcs.Count > 0) log_($"zone: {name} loaded {info.Npcs.Count} NPCs");
            LoadMobs(info);
            LoadZoneScripts(name);
            z = (info, ParseIp(ip), port);
            zones_[id] = z;
            BattlefieldLoadHandler(info);
            StartNpcs(info);
            StartMobs(info);
            ZoneLuaOnZoneInitialize(info, z.Ip);
            WeatherInitialize(info);
            return z;
        }

        // yaml::resolveEnum for a xi::ZoneId: the zone's folder name
        private ushort? ZoneIdByName(string name)
        {
            if (zoneIds_ == null)
            {
                zoneIds_ = new Dictionary<string, ushort>();
                var r = Exec("SELECT zoneid, name FROM zone_settings");
                while (r != null && r.Next()) zoneIds_.TryAdd(ZoneInfo.FolderName(r.Get<string>("name")), r.Get<ushort>("zoneid"));
            }
            return zoneIds_.TryGetValue(name, out var id) ? id : null;
        }

        // str2ip: the dotted address in network order (first octet in the low byte), 0 when it does not parse
        private static uint ParseIp(string ip)
        {
            var b = IPAddress.TryParse(ip, out var addr) ? addr.GetAddressBytes() : new byte[4];
            return (uint)(b[0] | b[1] << 8 | b[2] << 16 | b[3] << 24);
        }

        // zoneutils::GetZoneIPP: zone_settings' address, read again for each zone change since the zone may be another
        // map process's (not loaded here); 0 for a zone that is closed to chars. A zone this process holds is up by
        // then, as C++ loads them all at its start (here on first use); under --lazy only a loaded one is, IsZoneReady
        // loading the rest.
        private ulong ZoneIpp(ushort id)
        {
            if (IsZoneOnThisProcess(id)) Zone(id);
            var r = Exec("SELECT zoneip, zoneport FROM zone_settings WHERE zoneid = ?", BoundValue.Of(id));
            if (r == null || !r.Next())
            {
                log_($"zoneutils::GetZoneIPP: Cannot find zone {id}");
                return 0;
            }
            uint ip = ParseIp(r.Get<string>("zoneip"));
            ushort port = r.Get<ushort>("zoneport");
            return ip == 0 || port == 0 ? 0 : MapIpp.Make(ip, port);
        }

        // zoneutils::GetZonesAssignedToThisProcess: the zones whose zone_settings address is ProcessIpp, or every zone
        // without one (read once: ProcessIpp is set before the first lookup)
        private HashSet<ushort> assignedZones_;

        public IReadOnlyCollection<ushort> ZonesAssignedToThisProcess()
        {
            if (assignedZones_ != null) return assignedZones_;
            assignedZones_ = new HashSet<ushort>();
            var r = Exec("SELECT zoneid FROM zone_settings WHERE ? = 0 OR (zoneip = ? AND zoneport = ?)",
                         BoundValue.Of(MapIpp.Ip(ProcessIpp)), BoundValue.Of(MapIpp.ToString(ProcessIpp).Split(':')[0]),
                         BoundValue.Of(MapIpp.Port(ProcessIpp)));
            while (r != null && r.Next()) assignedZones_.Add(r.Get<ushort>("zoneid"));
            return assignedZones_;
        }

        // zoneutils::GetZone != nullptr: a map process holds the zones assigned to it (loaded here on first use); under
        // --lazy g_PZoneList has only what EnsureZoneLoaded / IsZoneReady loaded so far, whichever process it belongs to
        private bool IsZoneOnThisProcess(ushort id) =>
            LazyZones ? zones_.ContainsKey(id) : ProcessIpp == 0 || ((HashSet<ushort>)ZonesAssignedToThisProcess()).Contains(id);

        // zoneutils::EnsureZoneLoaded (recv_parse, before the 0x00A puts the char in it): under --lazy the char's zone
        // loads now, managed by this process or not
        public void EnsureZoneLoaded(ushort id)
        {
            if (!LazyZones || zones_.ContainsKey(id)) return;
            Zone(id);
        }

        // zoneutils::IsZoneReady (the zone tick, before a zone change or a home point, and before an instance loads):
        // under --lazy a zone this process manages loads now (lazyLoad.asyncMode is hardcoded off); another process's
        // is left to it
        private bool IsZoneReady(ushort id)
        {
            if (zones_.ContainsKey(id) || !LazyZones) return true;
            if (!((HashSet<ushort>)ZonesAssignedToThisProcess()).Contains(id)) return true;
            Zone(id);
            return true;
        }

        // zoneutils::GetZone, as zoneutils::GetEntity uses it: a zone this process holds (loaded on first use), null for
        // another process's (its entities are not here)
        private ZoneInfo HeldZone(ushort id) => IsZoneOnThisProcess(id) ? Zone(id).Info : null;

        // whether this process has loaded the zone (for the verify suites)
        public bool IsZoneLoaded(ushort id) => zones_.ContainsKey(id);

        // xi::data::loadZoneFile: a zone's zone.yaml settings without loading the zone, which may be another process's
        private readonly Dictionary<ushort, ZoneInfo> zoneFiles_ = new Dictionary<ushort, ZoneInfo>();

        private ZoneInfo ZoneFile(ushort id)
        {
            if (zones_.TryGetValue(id, out var z)) return z.Info;
            if (zoneFiles_.TryGetValue(id, out var info)) return info;
            var r = Exec("SELECT name FROM zone_settings WHERE zoneid = ?", BoundValue.Of(id));
            string name = r != null && r.Next() ? r.Get<string>("name") : "";
            return zoneFiles_[id] = ZoneInfo.Load(Path.Combine(dataDir_, "zones"), id, name, ZoneIdByName);
        }

        // zoneutils::IsZoneAtPlayerCap: the sessions whose char stands in the zone, against the cap less the slots kept
        // for GMs; an instanced zone has no cap
        private bool IsZoneAtPlayerCap(ushort zoneId, bool isGm)
        {
            var cfg = ChatCfg;
            int cap = (ushort)cfg.ZonePlayerCap;
            if (cap == 0) return false;
            int reserved = (ushort)cfg.ZonePlayerGmReserved;
            int threshold = isGm ? cap : cap > reserved ? cap - reserved : 0;
            if (ZoneFile(zoneId).Types.Contains("instanced")) return false;
            var r = Exec("SELECT COUNT(*) AS pop FROM accounts_sessions s JOIN chars c ON c.charid = s.charid WHERE c.pos_zone = ?",
                         BoundValue.Of(zoneId));
            return r != null && r.Next() && r.Get<uint>("pop") >= threshold;
        }

        // unity_system: the standings 0x063 sends
        private UnityStandings LoadUnity()
        {
            var u = new UnityStandings();
            var r = Exec("SELECT leader, members_current, points_current, members_prev, points_prev FROM unity_system");
            while (r != null && r.Next())
            {
                int i = r.Get<int>("leader") - 1;
                if (i < 0 || i >= 11) continue;
                u.MembersCurrent[i] = r.Get<int>("members_current");
                u.PointsCurrent[i] = r.Get<double>("points_current");
                u.MembersPrevious[i] = r.Get<int>("members_prev");
                u.PointsPrevious[i] = r.Get<double>("points_prev");
            }
            return u;
        }

        // MapNetwork.OnLogin: charutils::LoadChar
        public void OnLogin(MapSession s)
        {
            var c = loader_.Load(s.CharId);
            if (c == null) log_($"zone: char {s.CharId} could not be loaded");
            else
            {
                c.LoginMs = NowMs();
                c.Clock = () => NowMs();
                // charutils::LoadChar empties the recycle bin after the equipment
                EmptyRecycleBin(c);
                LoadPetZoningCharVars(c);
                ZoneLuaOnCharLoad(c);
                RoeOnCharLoad(c);
                JoinEquippedLinkshells(c);
                // the 0x00A handler puts the char in this zone, so it has to be loaded before it gets there
                EnsureZoneLoaded(c.Destination);
                // a new session (a login, or a char handed over by another map process) whose char comes from another
                // zone: the process it left closes the session it kept for it
                if (s.Blowfish.Status == BlowfishStatus.Waiting && c.Destination != c.PrevZone) SendIpc(new XI.Ipc.KillSession(s.CharId));
            }
            s.Char = c;
        }

        // GP_CLI_COMMAND_CHARREQ: the client asks for an entity it has no data for, e.g. a cutscene actor; until the
        // answer arrives the cutscene waits. Self, other chars and NPCs are the entities here.
        private void OnCharReq(MapSession s, CharData c, ushort actIndex)
        {
            var z = AreaOf(c);
            if (actIndex == c.TargId)
            {
                UpdateCharPacket(s, c, EntityUpdate.Spawn, LoginPackets.UpdateAllChar);
                s.PacketList.AddLast(LoginPackets.ServerStatus(c, z, UnixNow()));
                return;
            }
            if (CharByTargId(c, actIndex) is CharData other)
            {
                if (other.GmHidden)
                {
                    log_($"zone: Player {c.Name} requested information about a hidden GM ({other.Name}) using targid {actIndex}");
                    return;
                }
                UpdateCharPacket(s, other, EntityUpdate.Spawn, LoginPackets.UpdateAllChar);
                return;
            }
            foreach (var n in z.Npcs)
            {
                if (n.TargId != actIndex) continue;
                log_($"zone: {c.Name} requested entity {actIndex} ({n.Name})");
                // a mog house's onZoneIn cutscene asks for its Moogle, which stays hidden (SpawnConditionalNPCs' way:
                // shown for the packet, hidden again)
                if (c.MoghouseId == c.Id && n.Status == NpcEntity.StatusDisappear && IsMogHouseMoogle(n))
                {
                    n.Status = NpcEntity.StatusNormal;
                    UpdateEntityPacket(s, n, EntityUpdate.Spawn, EntityUpdatePacket.UpdateAllMob);
                    n.Status = NpcEntity.StatusDisappear;
                    return;
                }
                UpdateEntityPacket(s, n, EntityUpdate.Spawn, EntityUpdatePacket.UpdateAllMob);
                return;
            }
            // GetEntity(TYPE_NPC | TYPE_PC | TYPE_SHIP) leaves mobs out, but a mob the client asks for is answered here
            // so a cutscene that names one does not wait on it
            foreach (var m in z.Mobs)
            {
                if (m.TargId != actIndex) continue;
                UpdateEntityPacket(s, m, EntityUpdate.Spawn, EntityUpdatePacket.UpdateAllMob);
                return;
            }
            log_($"zone: Could not look up entity <{actIndex}, {((4096 + c.Zone) << 12) + actIndex}> in zone {z.Name} ({c.Zone})");
        }

        // ---- NPC spawns -------------------------------------------------------------------------------------------
        // CZoneEntities::SpawnNPCs: the spawn list follows what is visible from where the char stands; a char in a mog
        // house gets none. No spatial grid (a zone has at most a few hundred NPCs).

        private const float EntityRenderDistance = 50.0f;

        private static bool InRange(CharData c, NpcEntity n)
        {
            float dx = c.X - n.X, dy = c.Y - n.Y, dz = c.Z - n.Z;
            return dx * dx + dy * dy + dz * dz <= EntityRenderDistance * EntityRenderDistance;
        }

        // a transport (ship model) spawns by proximity unless something else drives it; an NPC when it is shown and
        // in range, or always relevant
        private static bool Visible(CharData c, NpcEntity n)
        {
            if (n.Model == ModelType.Ship) return !n.AlwaysRelevant && InRange(c, n);
            bool shown = n.Status == NpcEntity.StatusNormal || n.Status == NpcEntity.StatusUpdate;
            return shown && (InRange(c, n) || n.AlwaysRelevant);
        }

        private void SpawnNpcs(MapSession s, CharData c)
        {
            if (c.MoghouseId != 0) return; // inMogHouse
            var zone = AreaOf(c);
            var gone = new List<uint>();
            foreach (var id in c.SpawnNpcList)
                if (!zone.NpcById.TryGetValue(id, out var n) || !Visible(c, n)) gone.Add(id);
            foreach (var id in gone)
            {
                c.SpawnNpcList.Remove(id);
                if (zone.NpcById.TryGetValue(id, out var n)) UpdateEntityPacket(s, n, EntityUpdate.Despawn, 0);
            }
            foreach (var n in zone.Npcs)
            {
                if (c.SpawnNpcList.Contains(n.Id) || !Visible(c, n)) continue;
                c.SpawnNpcList.Add(n.Id);
                UpdateEntityPacket(s, n, EntityUpdate.Spawn, EntityUpdatePacket.UpdateAllMob);
            }
        }

        // CCharEntity::updateEntityPacket: an update for an entity whose 0x00E is still waiting to be sent is merged
        // into that packet rather than queued twice
        private static void UpdateEntityPacket(MapSession s, NpcEntity n, EntityUpdate type, byte updatemask)
        {
            foreach (var pending in s.PacketList)
            {
                if (pending.GetOpcode() == 0x00E && BitConverter.ToUInt32(pending.Buffer, 4) == n.Id)
                {
                    EntityUpdatePacket.UpdateWith(pending, n, type, updatemask);
                    return;
                }
            }
            s.PacketList.AddLast(EntityUpdatePacket.Npc(n, type, updatemask));
        }

        // ---- /heal and /logout -----------------------------------------------------------------------------------
        // Both are status effects: HEALING (scripts/effects/healing.lua sets the animation and heals on its ticks) and
        // LEAVEGAME (scripts/effects/leavegame.lua counts down on its 5 s ticks, its power holding the kind). The
        // scripts' leaveGame marks the char (status Shutdown) and the zone tick logs it out.

        private const byte AnimationNone = 0, AnimationHealing = 33;

        private readonly HashSet<MapSession> leaveGame_ = new HashSet<MapSession>();

        public Func<long> NowMs = () => Environment.TickCount64;

        // GP_SERV_COMMAND_SYSTEMMES (messageSystem)
        private static BasicPacket SystemMessage(ushort number, uint para = 0, uint para2 = 0)
        {
            var w = new PacketWriter(0x053, 16);
            w.U32(4, para);
            w.U32(8, para2);
            w.U16(12, number);
            return w.Packet;
        }

        // setAnimation: the UPDATE_HP the next PostTick turns into 0x0DF + 0x037; an event start or end sends it even
        // when the animation stays the same
        private void SetAnimation(MapSession s, CharData c, byte animation, bool always = false)
        {
            if (c.Animation == animation && !always) return;
            c.Animation = animation;
            s.PacketList.AddLast(LoginPackets.GroupAttr(c));
            s.PacketList.AddLast(LoginPackets.ServerStatus(c, AreaOf(c), UnixNow()));
        }

        // GP_CLI_COMMAND_CAMP: the effect's script changes the animation
        private void OnCamp(MapSession s, CharData c, uint mode)
        {
            bool healing = c.Animation == AnimationHealing;
            if (mode == (uint)CampMode.Off || (mode == (uint)CampMode.Toggle && healing))
            {
                c.StatusEffects.DelStatusEffectSilent(EffectId.Healing);
                return;
            }
            // ClearStateStack: an item use in progress is cancelled; an automaton disengages
            CancelItemUse(s, c);
            CancelAbility(c);
            AbortMagic(c, NowMs());
            c.InactiveState = null;
            if (c.PPet is AutomatonEntity { Engaged: true } automaton) MobDisengage(automaton, NowMs());
            if (c.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.Healing, 0, 0, ChatCfg.HealingTickDelay * 1000L, 0)))
                Push(s, CharStatus(c));
        }

        // GP_CLI_COMMAND_REQLOGOUT: Toggle / LogoutOn / Off / ShutdownOn for the logout and shutdown kinds; a crafted
        // LogoutOn for the shutdown kind (or ShutdownOn for logout) does nothing
        private void OnReqLogout(MapSession s, CharData c, ushort mode, ushort kind)
        {
            if (kind != (ushort)ReqlogoutKind.Logout && kind != (ushort)ReqlogoutKind.Shutdown) return;
            var k = (ReqlogoutKind)kind;
            var m = (ReqlogoutMode)mode;
            var existing = c.StatusEffects.GetStatusEffect(EffectId.LeaveGame);

            if (m == ReqlogoutMode.Off || (m == ReqlogoutMode.Toggle && existing != null))
            {
                if (existing != null) c.StatusEffects.DelStatusEffectSilent(EffectId.LeaveGame);
                return;
            }
            if ((k == ReqlogoutKind.Logout && m == ReqlogoutMode.ShutdownOn) || (k == ReqlogoutKind.Shutdown && m == ReqlogoutMode.LogoutOn)) return;

            // a different kind of leave game only changes the stored kind (the message)
            if (existing != null) existing.Power = kind;
            else c.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.LeaveGame, 0, kind, 5000, 0));
        }

        // ---- zone lines ------------------------------------------------------------------------------------------
        // GP_CLI_COMMAND_MAPRECT picks the destination (the char's status goes Disappear), and the zone tick sends the
        // char there (charutils::SendToZone + removeCharFromZone). The 0x00B names the destination's map process: this
        // one, where the client comes back with 0x00A on the same session, or another, which the CharZone IPC sent once
        // the 0x00B went out (OnZoneOut) has given a pending session for it. A mog house entrance (zmr* / zms*) zones
        // the char into its own mog house in the same zone; the universal exit (zmrq) by its exit mode
        // (ZoneHost.MogHouse.MogHouseExit).

        private const ushort MsgCouldNotEnter = 2, MsgCannotEnterAreaWhileMounted = 138;
        private const byte PosModeReset = 5;

        // char -> destination zone, for the chars whose status is Disappear
        private readonly Dictionary<MapSession, ushort> zoneChanges_ = new Dictionary<MapSession, ushort>();

        // GP_SERV_COMMAND_WPOS2
        private static BasicPacket Wpos2(CharData c, byte mode)
        {
            var w = new PacketWriter(0x065, 4 + S2cWpos2.SizeOf);
            w.F32(4 + S2cWpos2.OffX, c.X);
            w.F32(4 + S2cWpos2.OffY, c.Y);
            w.F32(4 + S2cWpos2.OffZ, c.Z);
            w.U32(4 + S2cWpos2.OffUniqueNo, c.Id);
            w.U16(4 + S2cWpos2.OffActIndex, c.TargId);
            w.U8(4 + S2cWpos2.OffMode, mode);
            w.U8(4 + S2cWpos2.OffDir, c.Rotation);
            return w.Packet;
        }

        // the client turns around where it stands and stays in the zone
        private void DenyZone(MapSession s, CharData c, ushort message = MsgCouldNotEnter)
        {
            c.Rotation += 128;
            s.PacketList.AddLast(SystemMessage(message));
            s.PacketList.AddLast(Wpos2(c, PosModeReset));
            zoneChanges_.Remove(s);
        }

        // GP_CLI_COMMAND_MAPRECT: the normal-status path for a zone line in this zone, a mog house entrance or its exit
        private void OnMapRect(MapSession s, CharData c, uint rectId, byte exitBit, byte exitMode)
        {
            if (s.ShuttingDown != 0 || zoneChanges_.ContainsKey(s)) return;
            ushort startingZone = c.Zone;
            var startingPos = (c.X, c.Y, c.Z, c.Rotation);
            ClearTrusts(c);
            string rect = ZoneLine.IdText(rectId);
            var (zone, _, _) = Zone(c.Zone);

            c.Boundary = 0;

            ushort destination;
            bool isMogHouseEntrance = rect.StartsWith("zmr") || rect.StartsWith("zms"); // zmr* classic cities; zms* WoTG [S] + Adoulin
            if (rect == "zmrq")
            {
                if (MogHouseExit(c, exitBit, exitMode, startingZone) is not ushort exitTo) return;
                destination = exitTo;
            }
            else
            {
                if (ZoneLineDestination(s, c, rectId, rect, zone, isMogHouseEntrance) is not ushort lineTo) return;
                destination = lineTo;
            }
            log_($"zone: zoning from zone {c.Zone} to zone {destination}: {c.Name}");
            s.PacketList.Clear();
            if (ZoneIpp(destination) == 0)
            {
                log_($"zone: {c.Name} requested zone ({destination}) returned IPP of 0");
                (c.X, c.Y, c.Z, c.Rotation) = startingPos;
                DenyZone(s, c);
                return;
            }
            zoneChanges_[s] = destination;
            SavePetForZoning(c);
        }

        // the zone line path: the line, the MON, distance, closed-zone, player-cap and rental chocobo checks; a mog house
        // entrance keeps the char in the zone, in its own mog house at the line's destination; null when denied
        private ushort? ZoneLineDestination(MapSession s, CharData c, uint rectId, string rect, ZoneInfo zone, bool isMogHouseEntrance)
        {
            if (!zone.ZoneLines.TryGetValue(rectId, out var line))
            {
                log_($"zone: zone line {rect} ({rectId}) not found in zone {c.Zone}");
                DenyZone(s, c);
                return null;
            }
            // no zone lines while MON
            if (c.Monstrosity != null)
            {
                DenyZone(s, c);
                return null;
            }
            // 38-42y distance limit observed
            float dx = c.X - line.OriginX, dz = c.Z - line.OriginZ;
            float dist = MathF.Sqrt(dx * dx + dz * dz);
            if (dist > 40.0f)
            {
                log_($"zone: {c.Name} too far from zone line {rect} ({dist:0.0}y)");
                DenyZone(s, c);
                return null;
            }
            if (ZoneIpp(line.DestinationZone) == 0)
            {
                log_($"zone: zone {line.DestinationZone} closed to chars");
                DenyZone(s, c);
                return null;
            }
            if (!isMogHouseEntrance && IsZoneAtPlayerCap(line.DestinationZone, c.AccountGmLevel > 0))
            {
                DenyZone(s, c);
                return null;
            }
            // a rental chocobo cannot be taken into a zone that does not permit riding
            if (c.StatusEffects.GetStatusEffect(EffectId.Mounted) is { Power: MountUtils.MountChocobo, SubPower: 0 } &&
                !ZoneFile(line.DestinationZone).CanUseMisc("mount"))
            {
                DenyZone(s, c, MsgCannotEnterAreaWhileMounted);
                return null;
            }

            ushort destination;
            if (isMogHouseEntrance)
            {
                // entering another char's mog house is MogHouseContainer::visit's
                c.MoghouseId = c.Id;
                (c.X, c.Y, c.Z, c.Rotation) = (line.DestX, line.DestY, line.DestZ, line.DestRotation);
                destination = c.Zone;
                c.PrevZone = c.Zone;
            }
            else
            {
                destination = line.DestinationZone;
                (c.X, c.Y, c.Z, c.Rotation) = line.NextSpawnPosition();
            }
            Exec("UPDATE chars SET pos_prevzonelineid = ? WHERE charid = ? LIMIT 1", BoundValue.Of(line.Id), BoundValue.Of(c.Id));
            return destination;
        }

        // charutils::SendToZone, then removeCharFromZone for a zoning char. False, the char left in its zone, while the
        // session waits for a zone change, the zone has no address or it is at its player cap (a zone line checked the
        // cap already, but the zone may have filled up since, and the other ways here did not)
        private bool SendToZone(MapSession s, CharData c, ushort destination)
        {
            if (s.Blowfish.Status == BlowfishStatus.PendingZone) return false;
            ulong ipp = ZoneIpp(destination);
            if (ipp == 0)
            {
                log_($"charutils::SendToZone : Invalid zoneId {destination}");
                return false;
            }
            if (IsZoneAtPlayerCap(destination, c.AccountGmLevel > 0))
            {
                log_($"charutils::SendToZone : zone {destination} at player cap, denying {c.Name} (gm={c.AccountGmLevel})");
                return false;
            }

            ForceSynthCritFail("SendToZone", s, c);
            CancelItemUse(s, c);
            CancelAbility(c);
            AbortMagic(c, NowMs());
            c.InactiveState = null;
            // a hooked catch is dropped on zone out
            if (c.HookedFish is { Hooked: true }) InterruptFishing(s, c);
            // a previous warp can get here, which could loop: the warp is un-requested
            c.RequestedWarp = WarpRequest.None;

            c.Destination = destination;
            // the destination's position: no persist sweep batch gathered before it may land after it (flushedAtTeardown)
            lock (persistLock_)
            {
                Exec("UPDATE chars SET pos_zone = ?, pos_prevzone = ?, pos_rot = ?, pos_x = ?, pos_y = ?, pos_z = ?, moghouse = ?, boundary = ? WHERE charid = ?",
                     BoundValue.Of(destination), BoundValue.Of(c.MoghouseId != 0 || destination == c.Zone ? c.PrevZone : c.Zone), BoundValue.Of(c.Rotation),
                     BoundValue.Of(c.X), BoundValue.Of(c.Y), BoundValue.Of(c.Z), BoundValue.Of(c.MoghouseId), BoundValue.Of(c.Boundary), BoundValue.Of(c.Id));
                PositionWritten(c.Id);
            }
            if (ShouldPetPersistThroughZoning(c)) SetPetZoningInfo(c);
            else ResetPetZoningInfo(c);
            // CZone::DecreaseZoneCounter: any zone-out closes the mog house
            CloseMogHouse(c);
            LeaveTreasurePool(c);
            RemoveCharPet(c);
            RemoveCharTrusts(c);
            BattlefieldCharZoneOut(c);
            EnmityCharZoneOut(c);
            WriteHistory(c);
            DespawnPc(c);
            // CZoneInstance::DecreaseZoneCounter: CharZoneOut runs while the char is still in its instance
            ZoneLuaCharZoneOut(c);
            InstanceCharZoneOut(c);
            LevelSyncCharZoneOut(c);
            LeaveUnityChat(c);
            LeaveLinkshells(c);
            CloseTrades(c);
            LeaveNominate(c);
            // a new CCharEntity in the destination starts without /blockaid or a /nominate cooldown
            c.BlockingAid = false;
            c.LastProposalCloseMs = null;
            c.WideScanTarget = null;
            s.ZoneIpp = 0;
            s.PacketList.AddLast(LogoutPacket.Create(LogoutState.Zonechange, ipp));

            s.Blowfish.Status = BlowfishStatus.PendingZone;
            s.ShuttingDown = 2;
            s.InZone = false;
            Exec("UPDATE char_stats SET zoning = 1 WHERE charid = ?", BoundValue.Of(c.Id));
            // CZone::CharZoneOut's loc.zone = nullptr: what flush's dropped effects run sees the destination
            c.ZonedOut = true;
            // persist::flush: a zone change keeps what survives it
            SaveStatusEffects(c, false);
            SaveOnZoneOut(c);
            return true;
        }

        // The zone tick: the effects (LEAVEGAME's ticks count down 25..5 and the sixth leaves the game), the zone
        // changes, then the chars that left the game log out
        public void Tick()
        {
            TickListeners();
            TickZoneScripts();
            TickDoors();
            TickSynths();
            TickItemUses();
            TickWeaponSkills();
            TickCharMobSkills();
            TickRanged();
            TickAbilities();
            TickCharMagic();
            TickCharInactive();
            TickStatusEffects();
            TickPersistSweep();
            TickVolatileServerVars();
            TickGameHour();
            TickTimeServer();
            TickTimeTriggers();
            TickWeather();
            TickMobs();
            TickNpcs();
            TickPets();
            TickTrusts();
            TickCombat();
            TickBattlefields();
            TickEntityActionQueues();
            TickRoe();
            TickGardening();
            TickTreasurePools();
            TickActionQueues();
            ProcessWarps();
            ProcessAfterZoneIn();
            ZoneLuaCheckTriggerAreas();
            TickInstances();
            TickTransports();
            foreach (var (s, destination) in new List<KeyValuePair<MapSession, ushort>>(zoneChanges_))
            {
                zoneChanges_.Remove(s);
                // a char that left the game logs out instead
                if (leaveGame_.Contains(s)) continue;
                if (s.Char is not CharData c) continue;
                // a zone closed meanwhile: dropped (the C++ takes the char out of its zone without a 0x00B)
                if (ZoneIpp(destination) == 0)
                {
                    log_($"zone: {c.Name} requested zone ({destination}) returned IPP of 0");
                    continue;
                }
                // under --lazy a destination this process manages loads first
                IsZoneReady(destination);
                s.PacketList.Clear();
                // m_charsToChangeZone keeps a char SendToZone turned away, for the next tick
                if (!SendToZone(s, c, destination)) zoneChanges_[s] = destination;
            }
            ProcessPartyReloads();

            // CZoneEntities::ZoneServer: a char the scripts' leaveGame marked shuts down
            foreach (var s in new List<MapSession>(leaveGame_))
            {
                leaveGame_.Remove(s);
                if (s.Char is not CharData c || s.ShuttingDown != 0) continue;
                s.PacketList.Clear();
                Logout(s, c);
            }
        }

        // charutils::ForceLogout (SendDisconnect) + removeCharFromZone for a shutting-down char: the client gets 0x00B,
        // the network layer marks the zone-out and closes the session once it has been sent
        private void Logout(MapSession s, CharData c)
        {
            // CZone::DecreaseZoneCounter: a synth in progress is lost
            ForceSynthCritFail("DecreaseZoneCounter", s, c);
            CancelItemUse(s, c);
            CancelAbility(c);
            AbortMagic(c, NowMs());
            c.InactiveState = null;
            // a hooked catch is dropped on zone out
            if (c.HookedFish is { Hooked: true }) InterruptFishing(s, c);
            CloseMogHouse(c);
            LeaveTreasurePool(c);
            RemoveCharPet(c);
            RemoveCharTrusts(c);
            BattlefieldCharZoneOut(c);
            EnmityCharZoneOut(c);
            WriteHistory(c);
            DespawnPc(c);
            // CZoneInstance::DecreaseZoneCounter: CharZoneOut runs while the char is still in its instance
            ZoneLuaCharZoneOut(c);
            InstanceCharZoneOut(c);
            LevelSyncCharZoneOut(c);
            LeaveUnityChat(c);
            LeaveLinkshells(c);
            CloseTrades(c);
            LeaveNominate(c);
            s.PacketList.Clear();
            SavePosition(c);
            s.PacketList.AddLast(LogoutPacket.Create(LogoutState.Logout, 0));

            s.Blowfish.Status = BlowfishStatus.PendingZone;
            s.ShuttingDown = 1;
            s.InZone = false;
            Exec("UPDATE char_stats SET zoning = 0 WHERE charid = ?", BoundValue.Of(c.Id));
            SaveStatusEffects(c, true);
            SaveOnZoneOut(c);
            charSessions_.Remove(c);
            // ~CCharEntity: the char's CAIContainer goes, its action queue, Lua timers and (P10) listeners with it
            ClearActionQueues(c);
            ReleaseListeners(c);
            ResetEntityData(c, destroyed: true);
            log_($"zone: {c.Name} ({c.Id}) logged out at ({c.X}, {c.Y}, {c.Z})");
        }

        // charutils::removeCharFromZone's saves (play time, stats, the main job's EXP, eminence, last logout), then
        // ~CCharEntity's SaveDeathTime for a dead char
        private void SaveOnZoneOut(CharData c)
        {
            SavePlayTime(c);
            SaveCharStats(c);
            SaveCharExp(c, c.MJob);
            SaveEminenceData(c);
            Exec("UPDATE chars SET last_logout = CURRENT_TIMESTAMP WHERE charid = ?", BoundValue.Of(c.Id));
            if (c.Hp <= 0) SaveDeathTime(c);
        }

        // charutils::SaveDeathTime: how long the char has been dead, which the next login takes up again
        private void SaveDeathTime(CharData c) =>
            Exec("UPDATE char_stats SET death = ? WHERE charid = ? LIMIT 1", BoundValue.Of((uint)(c.MsSinceDeath / 1000)), BoundValue.Of(c.Id));

        // charutils::loadDeathTimestamp: a char that logs in dead takes up its death where it left it (the death
        // state only, CCharEntity::Die(60 min less the time already dead): no defeat, no EXP lost again). A char kept
        // across a same-process zone change still has it.
        private void LoadDeathTimestamp(CharData c)
        {
            if (c.Hp != 0 || c.DeathAtMs != 0) return;
            var rs = Exec("SELECT death FROM char_stats WHERE charid = ? LIMIT 1", BoundValue.Of(c.Id));
            if (rs == null || !rs.Next()) return;
            long now = NowMs();
            CharEnterDeath(c, now, now - (long)rs.Get<uint>("death") * 1000);
        }

        // charutils::SavePlayTime
        private void SavePlayTime(CharData c) =>
            Exec("UPDATE chars SET playtime = ? WHERE charid = ? LIMIT 1", BoundValue.Of(PlayTime(c)), BoundValue.Of(c.Id));

        private static void Push(MapSession s, List<BasicPacket> packets)
        {
            foreach (var p in packets) s.PacketList.AddLast(p);
        }

        private static void Push(MapSession s, BasicPacket packet) => s.PacketList.AddLast(packet);

        // GP_CLI_COMMAND_LOGIN: place the char in its zone, record the session's addresses and send the login packets
        private void OnLoginPacket(MapSession s, CharData c, BasicPacket p)
        {
            // an out of sync zone-in: the client wants the server's packet ids to go on from its header sync
            if (p.GetSequence() > 1) s.ServerPacketId = p.GetSequence();
            s.PacketList.Clear();
            // CZone::IncreaseZoneCounter, once per zone-in: an act index of its own, then the chars nearby see it
            bool entering = !s.InZone;
            // GP_CLI_COMMAND_LOGIN: a zone this process does not hold (another map process's; under --lazy one that
            // EnsureZoneLoaded did not load) cannot be entered; the char is meant to go to its home point, but it is in
            // no zone for the zone tick to warp it (the C++ TODO)
            void DumpToHomePoint()
            {
                s.Blowfish.Status = BlowfishStatus.Accepted;
                s.ShuttingDown = 0;
                log_("GP_CLI_COMMAND_LOGIN: player tried to enter zone that was invalid or out of range");
                log_($"GP_CLI_COMMAND_LOGIN: dumping player `{c.Name}` to homepoint!");
                c.RequestedWarp = WarpRequest.HomePoint;
                if (ShouldPetPersistThroughZoning(c)) SetPetZoningInfo(c);
            }
            if (entering && (s.Blowfish.Status == BlowfishStatus.PendingZone || s.Blowfish.Status == BlowfishStatus.Waiting) &&
                !IsZoneOnThisProcess(c.Zone))
            {
                DumpToHomePoint();
                return;
            }
            // an instanced zone: the char's run, or out to the exit zone without one; neither that zone nor the previous
            // one on this process is a null CZone the C++ dereferences, taken here as the 0x00A's zone it does not hold
            if (entering && !InstanceCharAttach(c) && !IsZoneOnThisProcess(c.Zone))
            {
                log_($"GP_CLI_COMMAND_LOGIN: neither {c.Name}'s instance exit zone nor its previous zone {c.Zone} is on this process (a null zone in the C++)");
                DumpToHomePoint();
                return;
            }
            var (zone, zoneIp, _) = Zone(c.Zone);
            if (entering)
            {
                // the char is Disappear (CBaseEntity's constructor, or the zone change that brought it) until the
                // login packets go
                c.Status = NpcEntity.StatusDisappear;
                CheckMogHouseHostOnLogin(c);
                c.TargId = NewCharTargId(c);
                c.SpawnPcList.Clear();
                // CZone::IncreaseZoneCounter runs while the char is still Disappear: joining sends nothing
                JoinTreasurePool(c);
                // CZone::CharZoneIn: out of an instanced zone, the temporary items go (charutils::ClearTempItems)
                if (!IsInstancedZone(Zone(c.Zone).Info)) ClearTempItems(c);
            }
            s.InZone = true;
            if (entering)
            {
                InsertPc(c);
                // CZone::IncreaseZoneCounter (not CZoneInstance's): the pathos effects go silently
                if (!IsInstancedZone(Zone(c.Zone).Info)) c.StatusEffects.DelStatusEffectsByFlag(EffectFlag.OnZonePathos, EffectNotice.Silent);
                if (MogHouseHost(c) is { } mhHost && mhHost != c) AddMogHouseVisitor(mhHost, c);
                InstanceCharZoneIn(c);
                ZoneLuaCharZoneIn(c);
                BattlefieldCharZoneIn(c);
                // CZone::IncreaseZoneCounter: the party list on the next tick, the zone is marked visited and the zone's
                // latents rechecked
                c.ReloadParty = true;
                c.ZonesVisited[c.Zone >> 3] |= (byte)(1 << c.Zone % 8);
                MonstrosityZoneIn(c);
                c.Latents.CheckLatentsZone();
                ReadHistory(c);
            }

            // a new login or a same-process zone change: call zone in only once
            if (s.Blowfish.Status == BlowfishStatus.PendingZone || s.Blowfish.Status == BlowfishStatus.Waiting)
            {
                s.Blowfish.Status = BlowfishStatus.Accepted;
                s.ShuttingDown = 0;
                ZoneLuaOnZoneIn(c);
                ZoneLuaOnGameIn(c);
            }

            // charutils::updateSession
            Exec("UPDATE accounts_sessions SET targid = ?, server_addr = ?, client_addr = ?, client_port = ?, last_zoneout_time = 0 WHERE charid = ? LIMIT 1",
                 BoundValue.Of(c.TargId), BoundValue.Of(zoneIp), BoundValue.Of(MapIpp.Ip(s.ClientIpp)),
                 BoundValue.Of(MapIpp.Port(s.ClientIpp)), BoundValue.Of(c.Id));
            SavePosition(c);
            LoadDeathTimestamp(c);
            // charutils::SaveZonesVisited
            Exec("UPDATE chars SET zones = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.ZonesVisited), BoundValue.Of(c.Id));
            if (c.MoghouseId != 0) UpdateMannequins(s, c);
            // the pots grow without telling the client, whose item list is still to come
            if (InOwnMogHouse(c))
            {
                c.History[HistoryMhEntrances]++;
                UpdateGardening(s, c, false);
            }

            c.MogLockerAccess = HasMogLockerAccess(c);
            var mhOwner = MogHouseHost(c);
            if (c.MoghouseId != 0 && LoginPackets.MogHouseModelId(mhOwner ?? c) == null)
                log_($"Default case reached for GetMogHouseID by {(mhOwner ?? c).Name} ({c.Zone})");
            // an instance run's music overrides the zone's (GP_SERV_COMMAND_LOGIN's MusicNum)
            // the login packet loads a MON char's data it does not have yet
            if (c.MJob == Monstrosity.JobMon && c.Monstrosity == null) c.Monstrosity = Monstrosity.Load(db_, MonTable(), c.Id);
            Push(s, LoginPackets.OnLogin(c, AreaOf(c), UnixNow(), mhOwner, c.CurrentEvent));
            c.Status = NpcEntity.StatusNormal; // PChar->status = xi::Status::Normal
            c.AfterZoneInAtMs = NowMs() + 4000;
            log_($"zone: {c.Name} ({c.Id}) entered zone {c.Zone} {zone.Name} at ({c.X}, {c.Y}, {c.Z})");
        }

        // zoneutils::AfterZoneIn, queued 4 s after the login: a level restriction lifted outside a battlefield, the
        // Assist Channel notice, then luautils::AfterZoneIn
        private void ProcessAfterZoneIn()
        {
            long now = NowMs();
            foreach (var (c, s) in new List<KeyValuePair<CharData, MapSession>>(charSessions_))
            {
                if (c.AfterZoneInAtMs == 0 || now < c.AfterZoneInAtMs || !s.InZone || s.Char != c) continue;
                c.AfterZoneInAtMs = 0;
                if (c.PBattlefield == null || !c.PBattlefield.IsEntered(c)) UpdateCharLevelRestriction(c);
                AmanOnZoneIn(s, c);
                ZoneLuaAfterZoneIn(c);
            }
        }

        // CCharEntity::pendingPositionUpdate: a 0x05B that moves the char itself is pushed and not yet sent, so the
        // client's position reports are stale until it goes out
        private static bool PendingPositionUpdate(MapSession s, CharData c)
        {
            foreach (var p in s.PacketList)
                if (p.GetOpcode() == 0x05B && BitConverter.ToUInt32(p.Buffer, 0x10) == c.Id) return true;
            return false;
        }

        // GP_CLI_COMMAND_POS: x, then the height the server keeps as y, then z; a change is an UPDATE_POS for the chars
        // that see this one
        private static void OnPos(CharData c, BasicPacket p)
        {
            var b = p.Buffer;
            float x = BitConverter.ToSingle(b, C2sPos.OffX), height = BitConverter.ToSingle(b, C2sPos.OffZ), z = BitConverter.ToSingle(b, C2sPos.OffY);
            if (!float.IsFinite(x) || !float.IsFinite(height) || !float.IsFinite(z)) return;
            ushort facetarget = BitConverter.ToUInt16(b, C2sPos.OffFacetarget);
            byte dir = b[C2sPos.OffDir];
            bool moved = c.X != x || c.Y != height || c.Z != z || c.FaceTarget != facetarget || c.Rotation != dir;
            float dx = x - c.X, dy = height - c.Y, dz = z - c.Z;
            c.LastMoveDistance = MathF.Sqrt(dx * dx + dz * dz);
            c.X = x;
            c.Y = height;
            c.Z = z;
            c.Moving = BitConverter.ToUInt16(b, C2sPos.OffMoveFlame);
            c.Rotation = dir;
            c.FaceTarget = facetarget;
            if (moved)
            {
                c.UpdateMask |= CharStats.UpdatePos;
                c.Persist |= CharPersist.Position;
                // the rough amount of steps taken
                c.History[HistoryDistanceTravelled] += (uint)MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            }
        }

        // charutils::SaveCharPosition
        public void SavePosition(CharData c)
        {
            // newer than a persist sweep batch still on its way to the database (ZoneHost.StatusEffects WriteBatch)
            lock (persistLock_)
            {
                Exec("UPDATE chars SET pos_rot = ?, pos_x = ?, pos_y = ?, pos_z = ?, boundary = ? WHERE charid = ?",
                     BoundValue.Of(c.Rotation), BoundValue.Of(c.X), BoundValue.Of(c.Y), BoundValue.Of(c.Z), BoundValue.Of(c.Boundary), BoundValue.Of(c.Id));
                PositionWritten(c.Id);
            }
        }

        // MapSessionContainer::cleanupSessions' link-dead steps: 5 s without a client update flags the char (the
        // char_flags row the search server reads, the char packets' LinkDeadFlag), the next update clears it and saves
        // the char's stats. Either way the others see the change (UPDATE_HP) and, standing, the char's spawn list is
        // run again (SpawnPCs). A link-dead char sends no datagram to flush its mask, so its PostTick's update goes
        // out now. A char already out of its zone (a zone change or logout before its 0x00B) has no PostTick and no
        // zone (removeCharFromZone): only the row and the stats are written.
        public void OnLinkDead(MapSession s, bool stale)
        {
            if (s.Char is not CharData c || stale == c.IsLinkDead) return;
            Exec("UPDATE char_flags SET disconnecting = ? WHERE charid = ?", BoundValue.Of((byte)(stale ? 1 : 0)),
                 BoundValue.Of(s.CharId));
            c.IsLinkDead = stale;
            c.UpdateMask |= CharStats.UpdateHp;
            if (s.InZone && c.Status == NpcEntity.StatusNormal) SpawnPcs(s, c);
            if (!stale) SaveCharStats(c);
            if (s.InZone) SendUpdateMask(s, c);
        }

        // MapSessionContainer.OnCharTimeout: the client went away, keep where it was standing
        public void OnCharTimeout(MapSession s, bool otherMap)
        {
            leaveGame_.Remove(s);
            if (s.Char is not CharData c) return;
            ForceSynthCritFail("DecreaseZoneCounter", s, c);
            CancelItemUse(s, c);
            CancelAbility(c);
            AbortMagic(c, NowMs());
            c.InactiveState = null;
            // a hooked catch is dropped on zone out
            if (c.HookedFish is { Hooked: true }) InterruptFishing(s, c);
            CloseMogHouse(c);
            LeaveTreasurePool(c);
            RemoveCharPet(c);
            RemoveCharTrusts(c);
            BattlefieldCharZoneOut(c);
            EnmityCharZoneOut(c);
            WriteHistory(c);
            if (s.InZone)
            {
                DespawnPc(c);
                // CZoneInstance::DecreaseZoneCounter: CharZoneOut runs while the char is still in its instance
                ZoneLuaCharZoneOut(c);
                InstanceCharZoneOut(c);
            }
            LevelSyncCharZoneOut(c);
            LeaveUnityChat(c);
            LeaveLinkshells(c);
            CloseTrades(c);
            LeaveNominate(c);
            ReleaseFromParty(c);
            charSessions_.Remove(c);
            // ~CCharEntity: the char's CAIContainer goes, its action queue, Lua timers and (P10) listeners with it
            ClearActionQueues(c);
            ReleaseListeners(c);
            ResetEntityData(c, destroyed: true);
            SavePosition(c);
            // persist::flush on the way out, unless the char went on to another map server that now owns it
            if (!otherMap) SaveStatusEffects(c, true);
            SaveOnZoneOut(c);
            log_($"zone: {c.Name} ({c.Id}) timed out at ({c.X}, {c.Y}, {c.Z})");
        }
    }
}
