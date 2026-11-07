using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Script;

namespace XI.Zone
{
    // zone.yaml's transport schedule (data/datasets/zones/settings: convertTransports / convertPhases): the zone's one
    // ship and the runs it serves, each a cycle of phases from its offset, one of which may take whatever the cycle
    // leaves; the berth is the place "dock", the run's places the others
    public sealed class TransportData
    {
        public const byte StateArriving = 0, StateDocked = 1, StateDeparting = 2, StateCycling = 3, StateHolding = 4, StateClosing = 5;

        public sealed class Move
        {
            public float X, Y, Z;
            public byte Rotation;
            public uint After; // seconds into the phase
        }

        public sealed class Phase
        {
            public byte State, Animation;
            public uint Start, End; // seconds into the cycle
            public List<Move> Moves = new List<Move>();
            public uint? Hide;      // seconds into the phase after which the client stops drawing it
        }

        public string Name = "";
        public uint Ship;
        public string Door = "";
        public Move Dock;           // null on a decorative ship, which keeps wherever the zone put it
        public ushort Boundary;
        public List<ushort> Crossings = new List<ushort>();
        public uint Every, Offset, Disembark;
        public List<Phase> Phases = new List<Phase>();

        // the zone.yaml's `transport`, its runs in name order; none without one
        public static List<TransportData> Load(string zoneYaml, DataEnums enums, Func<string, ushort?> zoneIdByName)
        {
            var list = new List<TransportData>();
            if (!File.Exists(zoneYaml)) return list;
            var schedule = MiniYaml.Load(zoneYaml).MapOrNull("transport");
            if (schedule == null) return list;
            uint ship = U32(schedule.Str("ship"));
            var runs = schedule.MapOrNull("runs") ?? new Dictionary<string, object>();
            foreach (var name in runs.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (runs[name] is not Dictionary<string, object> source) continue;
                var t = new TransportData
                {
                    Name = name,
                    Ship = ship,
                    Door = source.Str("door") ?? "",
                    Boundary = (ushort)U32(source.Str("boundary")),
                    Every = U32(source.Str("every")),
                    Offset = U32(source.Str("offset")),
                    Disembark = U32(source.Str("disembark")),
                };
                foreach (var zone in source.List("voyage"))
                    t.Crossings.Add(zoneIdByName((string)zone) ?? throw new FormatException($"transport '{name}' crosses unknown zone '{zone}'"));

                var places = new Dictionary<string, Move>();
                if (source.List("dock") is { Count: > 0 } dock)
                {
                    var v = Floats(dock);
                    if (v.Count < 3 || v.Count > 4) throw new FormatException($"transport '{name}' position needs three values plus an optional facing");
                    t.Dock = new Move { X = v[0], Y = v[1], Z = v[2], Rotation = v.Count > 3 ? (byte)v[3] : (byte)0 };
                    places["dock"] = t.Dock;
                }
                foreach (var (place, at) in source.MapOrNull("places") ?? new Dictionary<string, object>())
                {
                    var v = Floats((List<object>)at);
                    places.TryAdd(place, new Move { X = v[0], Y = v[1], Z = v[2] });
                }
                t.Phases = ConvertPhases(source.List("phases"), t.Every, name, places, enums);
                list.Add(t);
            }
            return list;
        }

        // convertPhases: back to back from the offset; a run with no cycle holds its single phase forever
        private static List<Phase> ConvertPhases(List<object> source, uint every, string name, Dictionary<string, Move> places, DataEnums enums)
        {
            var phases = source.ConvertAll(o => (Dictionary<string, object>)o);
            if (phases.Count == 0) throw new FormatException($"transport '{name}' declares no phases");
            byte State(Dictionary<string, object> p) => (byte)enums.Resolve("transport_state", p.Str("state") ?? "");
            byte Animation(Dictionary<string, object> p) => p.Str("animation") is string a ? (byte)enums.Resolve("animation", a) : (byte)0;
            if (every == 0)
            {
                if (phases.Count != 1 || phases[0].Str("seconds") != null)
                    throw new FormatException($"transport '{name}' has no cycle length, so it needs exactly one phase with no length of its own");
                return new List<Phase> { new Phase { State = State(phases[0]), Animation = Animation(phases[0]), Start = 0, End = uint.MaxValue } };
            }

            int open = phases.Count(p => p.Str("seconds") == null);
            if (open > 1) throw new FormatException($"transport '{name}' leaves {open} phases without a length; at most one may");
            uint stated = 0;
            foreach (var p in phases) stated += U32(p.Str("seconds"));
            if (stated > every) throw new FormatException($"transport '{name}' phases run {stated}s, longer than its {every}s cycle");
            if (open == 0 && stated != every) throw new FormatException($"transport '{name}' phases run {stated}s, short of its {every}s cycle, and no phase is open-ended");

            var result = new List<Phase>();
            uint cursor = 0;
            foreach (var p in phases)
            {
                uint length = p.Str("seconds") is string s ? U32(s) : every - stated;
                var phase = new Phase
                {
                    State = State(p),
                    Animation = Animation(p),
                    Start = cursor,
                    End = cursor + length,
                    Hide = p.Str("hide") is string hide ? U32(hide) : null,
                };
                foreach (var m in p.List("moves"))
                {
                    var move = (Dictionary<string, object>)m;
                    string to = move.Str("to") ?? "";
                    if (!places.TryGetValue(to, out var where)) throw new FormatException($"transport '{name}' moves to '{to}', which is not one of its places");
                    phase.Moves.Add(new Move { X = where.X, Y = where.Y, Z = where.Z, Rotation = where.Rotation, After = U32(move.Str("after")) });
                }
                result.Add(phase);
                cursor += length;
            }
            return result;
        }

        private static uint U32(string v) => v == null ? 0 : uint.Parse(v, CultureInfo.InvariantCulture);

        private static List<float> Floats(List<object> l) => l.ConvertAll(o => float.Parse((string)o, CultureInfo.InvariantCulture));
    }

    // transports/ship.cpp, ship_handler.cpp and voyage.h: ShipHandler::InitializeShips (every zone's runs: the ship
    // registered where it docks, always relevant, put at its berth; a voyage per crossing for a run that docks) and
    // tick, every 400 ms on the Vana'diel timestamp: each ship enters the phase the cycle is in (arriving, docked with
    // the door swung open, closing, departing with the door shut and the riders in its boarding area collected
    // (TransportDepart), cycling or holding), is moved as the phase says and hidden once its departure has carried it
    // out of sight; a crossing zone with no leg carrying anyone puts everyone ashore (DisembarkAll), and once a voyage
    // has ended onTransportVoyageEnd runs. CZoneEntities::TransportDepart / DisembarkAll / SpawnTransport (the ship
    // shown on GAMEOK), and the instance forms (an instanced zone's runs see their own chars) by the char's zone.
    // InitializeShips is the host's to call at startup (the C++ skips it with lazy zones).
    public sealed partial class ZoneHost
    {
        private const long TransportTickMs = 400;     // kTransportTickInterval
        private const ushort ShipGroundFlag = 0x8000; // flags0's GroundFlag: never dropped onto the terrain
        private const ushort ShipMovTimeStill = 1, ShipMovTimeMoving = 7;

        private sealed class Ship
        {
            public string Name;
            public NpcEntity Npc, Door;
            public ushort DockZone, Boundary, PrevZone;
            public uint Offset, Every;
            public bool Relocates;
            public List<TransportData.Phase> Phases;
            public int? Phase, Move;
            public bool Hidden;
        }

        private sealed class Voyage
        {
            public ushort Zone;
            public uint Offset, Every, DisembarkFrom, BoardingEnds;

            public bool Carrying(uint now)
            {
                long elapsed = (long)now - Offset;
                long phase = elapsed % Every;
                long into = phase < 0 ? phase + Every : phase;
                // airship passenger windows wrap past the end of their cycle
                if (BoardingEnds <= DisembarkFrom) return into >= BoardingEnds && into < DisembarkFrom;
                return into >= BoardingEnds || into < DisembarkFrom;
            }

            public long LatestCompletedAt(uint now)
            {
                long elapsed = (long)now - Offset - DisembarkFrom;
                long sinceEnd = elapsed % Every;
                return now - (sinceEnd < 0 ? sinceEnd + Every : sinceEnd);
            }
        }

        // VoyageEndState: a crossing finishes when none of its windows is carrying anyone
        private sealed class VoyageEndState
        {
            private long? lastCompletedAt_;
            private bool pending_;

            public bool Update(long latestCompletedAt, bool carrying)
            {
                // startup and clock rewinds establish a baseline without completing a voyage
                if (lastCompletedAt_ == null || latestCompletedAt < lastCompletedAt_) pending_ = false;
                else if (latestCompletedAt > lastCompletedAt_) pending_ = true;
                lastCompletedAt_ = latestCompletedAt;
                if (!pending_ || carrying) return false;
                pending_ = false;
                return true;
            }
        }

        private List<Ship> ships_;
        private readonly List<Voyage> voyages_ = new List<Voyage>();
        private readonly Dictionary<ushort, VoyageEndState> voyageEndStates_ = new Dictionary<ushort, VoyageEndState>();
        private long nextTransportTickMs_;

        // earth_time::vanadiel_timestamp: real seconds since the Vana'diel epoch
        private uint VanadielTimestamp() => (uint)(UnixNow() - VanaTime.EpochUnixSec);

        // the runs ShipHandler::InitializeShips registered, by name
        public IReadOnlyList<string> TransportNames => ships_?.ConvertAll(s => s.Name) ?? new List<string>();

        // ShipHandler::InitializeShips: every zone's file, keeping whichever half is ours (a zone with an address)
        public void InitializeShips()
        {
            if (ships_ != null) throw new InvalidOperationException("ships are already initialized");
            ships_ = new List<Ship>();
            ZoneIdByName("");
            foreach (var (folder, zoneId) in zoneIds_.OrderBy(z => z.Value))
            {
                var entries = TransportData.Load(Path.Combine(dataDir_, "zones", folder, "zone.yaml"), enums_, ZoneIdByName);
                foreach (var entry in entries)
                {
                    RegisterShip(entry, zoneId);
                    RegisterVoyage(entry);
                }
            }
        }

        // ship_handler's ownsZone: a zone this process holds, open to chars
        private bool OwnsZone(ushort zone) => IsZoneOnThisProcess(zone) && Zone(zone).Ip != 0;

        private void RegisterShip(TransportData entry, ushort dockZone)
        {
            if (!OwnsZone(dockZone)) return;
            var shipZone = Zone((ushort)(entry.Ship >> 12 & 0xFFF)).Info;
            if (!shipZone.NpcById.TryGetValue(entry.Ship, out var ship) || ship.Model != ModelType.Ship)
                throw new InvalidOperationException($"transport {entry.Name} names ship {entry.Ship}, which is not in {Zone(dockZone).Info.Name}");
            NpcEntity door = null;
            if (entry.Door != "")
            {
                door = Zone(dockZone).Info.Npcs.Find(n => n.Name == entry.Door)
                    ?? throw new InvalidOperationException($"transport {entry.Name} names door '{entry.Door}', which is not in {Zone(dockZone).Info.Name}");
            }
            ship.AlwaysRelevant = true;
            var s = new Ship
            {
                Name = entry.Name,
                Npc = ship,
                Door = door,
                DockZone = dockZone,
                Boundary = entry.Boundary,
                PrevZone = entry.Crossings.Count == 0 ? (ushort)0 : entry.Crossings[0],
                Offset = entry.Offset,
                Every = entry.Every,
                Relocates = entry.Phases.Any(p => p.Moves.Count != 0),
                Phases = entry.Phases,
            };
            // start it at its berth so the phases only have to say when it leaves
            if (entry.Dock != null) ShipMoveTo(s, entry.Dock);
            ships_.Add(s);
        }

        // registerVoyage: a run with a docked phase carries riders through each zone it crosses
        private void RegisterVoyage(TransportData entry)
        {
            var docked = entry.Phases.Find(p => p.State == TransportData.StateDocked);
            if (docked == null) return;
            foreach (var zone in entry.Crossings)
            {
                if (!OwnsZone(zone)) continue;
                if (entry.Disembark == 0)
                    throw new InvalidOperationException($"transport {entry.Name} carries riders through {Zone(zone).Info.Name} but states no disembark point");
                voyages_.Add(new Voyage { Zone = zone, Offset = entry.Offset, Every = entry.Every, DisembarkFrom = entry.Disembark, BoardingEnds = docked.End });
            }
        }

        // ---- Ship ---------------------------------------------------------------------------------------------------

        // moveTo: never despawned; GroundFlag, and the MovTime the client slides the model over
        private static void ShipMoveTo(Ship s, TransportData.Move where)
        {
            (s.Npc.X, s.Npc.Y, s.Npc.Z, s.Npc.Rotation) = (where.X, where.Y, where.Z, where.Rotation);
            s.Npc.Status = NpcEntity.StatusNormal;
            s.Npc.Moving = (ushort)(ShipGroundFlag | (s.Relocates ? ShipMovTimeMoving : ShipMovTimeStill));
        }

        // animate: the client plays forward from the stamp, so a ship met mid-phase picks it up part-way
        private void ShipAnimate(Ship s, byte animation, uint phaseStart)
        {
            s.Npc.Animation = animation;
            s.Npc.LocalVars["TransportTimestamp"] = phaseStart;
        }

        private static void ShipSetDoor(Ship s, byte animation)
        {
            if (s.Door != null) s.Door.Animation = animation;
        }

        private void ShipSwingDoor(Ship s, byte animation)
        {
            if (s.Door == null) return;
            ShipSetDoor(s, animation);
            BroadcastTransportUpdate(s.DockZone, s.Door, EntityUpdatePacket.UpdateCombat);
        }

        private void ShipUpdate(Ship s) => BroadcastTransportUpdate(s.DockZone, s.Npc, EntityUpdatePacket.UpdateCombat);

        // UpdateEntityPacket(.., alwaysInclude): every char in the zone
        private void BroadcastTransportUpdate(ushort zone, NpcEntity n, byte updatemask, EntityUpdate type = EntityUpdate.Update)
        {
            foreach (var (c, s) in charSessions_)
                if (c.Zone == zone && c.Instance == null) UpdateEntityPacket(s, n, type, updatemask);
        }

        // enter: everything a phase implies, so a cold start lands in the right state
        private void ShipEnter(Ship s, int index, uint now, uint into)
        {
            uint cycleStart = now - into;
            var next = s.Phases[index];
            uint phaseStart = cycleStart + next.Start;
            // a docked ship is still playing the tail of its arrival, so it times from when arrival began
            var arriving = s.Phases.Find(p => p.State == TransportData.StateArriving);
            switch (next.State)
            {
                case TransportData.StateArriving:
                    ShipAnimate(s, next.Animation, phaseStart);
                    ShipSetDoor(s, AnimationCloseDoor);
                    break;
                case TransportData.StateDocked:
                    if (arriving != null) ShipAnimate(s, arriving.Animation, cycleStart + arriving.Start);
                    ShipSwingDoor(s, AnimationOpenDoor);
                    break;
                case TransportData.StateClosing:
                    // retail shuts the door a few seconds before getting under way
                    if (arriving != null) ShipAnimate(s, arriving.Animation, cycleStart + arriving.Start);
                    ShipSwingDoor(s, AnimationCloseDoor);
                    break;
                case TransportData.StateDeparting:
                    ShipAnimate(s, next.Animation, phaseStart);
                    ShipSetDoor(s, AnimationCloseDoor);
                    // boundary zero is "not in a sub-area": the barge and the manaclipper sort out their riders themselves
                    TransportDepart(s.DockZone, s.Boundary, s.PrevZone, s.Name);
                    break;
                case TransportData.StateCycling:
                case TransportData.StateHolding:
                    // neither carries anyone; a holding ship is entered once, at startup, and stamps that moment
                    s.Npc.Status = NpcEntity.StatusNormal;
                    ShipAnimate(s, next.Animation, next.State == TransportData.StateHolding ? now : phaseStart);
                    break;
            }
            s.Phase = index;
            s.Move = null;
            ShipUpdate(s);
        }

        // conceal: stop drawing it once the departure has carried it out of sight
        private void ShipConceal(Ship s, uint intoPhase)
        {
            var current = s.Phases[s.Phase.Value];
            bool away = current.Hide is uint hide && intoPhase >= hide;
            if (away == s.Hidden) return;
            s.Hidden = away;
            s.Npc.Status = away ? NpcEntity.StatusDisappear : NpcEntity.StatusNormal;
            ShipUpdate(s);
        }

        // place: the last of the phase's moves that is due; a position only goes out when the ship goes somewhere
        private void ShipPlace(Ship s, uint intoPhase)
        {
            var moves = s.Phases[s.Phase.Value].Moves;
            int? wanted = null;
            for (int step = 0; step < moves.Count; step++)
                if (intoPhase >= moves[step].After) wanted = step;
            if (wanted == null || wanted == s.Move) return;
            s.Move = wanted;
            ShipMoveTo(s, moves[wanted.Value]);
            ShipUpdate(s);
        }

        // the transport task, every 400 ms: ShipHandler::tick, then ElevatorHandler::tick
        private void TickTransports()
        {
            long nowMs = NowMs();
            if (nowMs < nextTransportTickMs_) return;
            nextTransportTickMs_ = nowMs + TransportTickMs;
            TickShips();
            TickElevators();
        }

        // ShipHandler::tick
        private void TickShips()
        {
            if (ships_ == null) return;
            uint now = VanadielTimestamp();

            foreach (var s in ships_)
            {
                uint into = s.Every != 0 ? (now - s.Offset) % s.Every : now - s.Offset;
                int index = s.Phases.FindIndex(p => into >= p.Start && into < p.End);
                if (index < 0) continue; // the phases cover the whole cycle
                if (index != s.Phase) ShipEnter(s, index, now, into);
                uint intoPhase = into - s.Phases[s.Phase.Value].Start;
                ShipPlace(s, intoPhase);
                ShipConceal(s, intoPhase);
            }

            // both directions cross the same zone, so it is only empty when neither leg has anyone aboard
            var updated = new List<ushort>();
            foreach (var voyage in voyages_)
            {
                if (updated.Contains(voyage.Zone)) continue;
                updated.Add(voyage.Zone);
                bool busy = voyages_.Any(leg => leg.Zone == voyage.Zone && leg.Carrying(now));
                if (!busy) DisembarkAll(voyage.Zone);
                // remember scheduled ends even when a tick skips the whole interval between runs
                long latestCompletedAt = voyage.LatestCompletedAt(now);
                foreach (var leg in voyages_)
                    if (leg.Zone == voyage.Zone) latestCompletedAt = Math.Max(latestCompletedAt, leg.LatestCompletedAt(now));
                if (!voyageEndStates_.TryGetValue(voyage.Zone, out var state)) voyageEndStates_[voyage.Zone] = state = new VoyageEndState();
                if (state.Update(latestCompletedAt, busy)) OnTransportVoyageEnd(voyage.Zone);
            }
        }

        // ---- CZoneEntities ------------------------------------------------------------------------------------------

        // TransportDepart: the riders in the boarding area
        private void TransportDepart(ushort zone, ushort boundary, ushort prevZone, string transport)
        {
            foreach (var (c, _) in new List<KeyValuePair<CharData, MapSession>>(charSessions_))
                if (c.Zone == zone && ZoneLuaChar(c).TriggerAreaIds.Contains(boundary)) SendTransportEvent(c, prevZone, transport);
        }

        // DisembarkAll: everyone not already watching the event that takes them off (it runs every tick until the zone
        // is empty)
        private void DisembarkAll(ushort zone)
        {
            foreach (var (c, _) in new List<KeyValuePair<CharData, MapSession>>(charSessions_))
                if (c.Zone == zone && !c.IsNpcLocked) SendTransportEvent(c, zone, "");
        }

        // sendTransportEvent: a char who talked to someone aboard has the wrong event target, which would load that
        // NPC's script and leave the char on a black screen, so the prep goes back to the zone's
        private void SendTransportEvent(CharData c, ushort prevZone, string transport)
        {
            var prep = c.EventPreparation;
            if (prep.TargetEntity != null)
            {
                prep.TargetEntity = null;
                int deleteStart = prep.ScriptFile.IndexOf("npcs/", StringComparison.Ordinal);
                int deleteEnd = prep.ScriptFile.IndexOf(".lua", StringComparison.Ordinal);
                if (deleteStart >= 0 && deleteEnd >= 0)
                    prep.ScriptFile = prep.ScriptFile.Substring(0, deleteStart) + "Zone" + prep.ScriptFile.Substring(deleteEnd);
            }
            OnTransportEvent(c, prevZone, transport);
        }

        // SpawnTransport (GAMEOK): the always-relevant ships the proximity sync leaves out
        private void SpawnTransport(MapSession s, CharData c)
        {
            foreach (var n in AreaOf(c).Npcs)
                if (n.Model == ModelType.Ship && n.AlwaysRelevant) UpdateEntityPacket(s, n, EntityUpdate.Spawn, EntityUpdatePacket.UpdateAllMob);
        }

        // ---- luautils -----------------------------------------------------------------------------------------------

        // OnTransportEvent: the char's zone's onTransportEvent(player, prevZoneId, transport)
        private void OnTransportEvent(CharData c, ushort prevZone, string transport)
        {
            if (lua_ == null || scripts_ == null) return;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(ZoneLuaFile(AreaOf(c)), "onTransportEvent");
                if (lua_.IsNil(-1)) return;
                lua_.PushObject(c);
                lua_.Push((double)prevZone);
                lua_.Push(transport);
                lua_.Call(3, 0);
            }
            catch (LuaException e)
            {
                log_($"luautils::onTransportEvent: {e.Message}");
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // OnTransportVoyageEnd: the crossing zone's onTransportVoyageEnd(zone)
        private void OnTransportVoyageEnd(ushort zone)
        {
            if (lua_ == null || scripts_ == null) return;
            var info = Zone(zone).Info;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(ZoneLuaFile(info), "onTransportVoyageEnd");
                if (lua_.IsNil(-1)) return;
                lua_.PushObject(info, WeatherZoneLuaType);
                lua_.Call(1, 0);
            }
            catch (LuaException e)
            {
                log_($"luautils::onTransportVoyageEnd: {e.Message}");
            }
            finally
            {
                lua_.SetTop(top);
            }
        }
    }
}
